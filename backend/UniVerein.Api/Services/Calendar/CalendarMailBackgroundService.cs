using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Models;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Services.Calendar;

public class CalendarMailBackgroundService : BackgroundService
{
    private const int MaxJobsPerBatch = 50;

    private readonly CalendarMailQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;

    public CalendarMailBackgroundService(CalendarMailQueue queue, IServiceScopeFactory scopeFactory)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            List<CalendarMailJob> jobs;
            try
            {
                jobs = [await _queue.DequeueAsync(stoppingToken)];
            }
            catch (OperationCanceledException)
            {
                return;
            }

            while (jobs.Count < MaxJobsPerBatch && _queue.TryDequeue(out CalendarMailJob? next))
                jobs.Add(next);

            try
            {
                await SendAsync(jobs);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "CalendarMailBackgroundService: Error while sending calendar mails.");
            }
        }
    }

    private async Task SendAsync(List<CalendarMailJob> jobs)
    {
        List<PendingMail> pending = [];
        foreach (CalendarMailJob job in jobs)
        {
            try
            {
                List<(string Username, string Email)> recipients = await ResolveRecipientsWithRetryAsync(job);
                pending.AddRange(recipients.Select(r =>
                    new PendingMail(r.Username, job.SettingType, new Recipient { Email = r.Email }, job.Request)));
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"CalendarMailBackgroundService: Error while resolving recipients for '{job.SettingType}' mail.");
            }
        }

        if (pending.Count == 0)
            return;

        using IServiceScope scope = _scopeFactory.CreateScope();
        MailService mailService = scope.ServiceProvider.GetRequiredService<MailService>();

        List<EmailResult> results = await mailService.SendBatchAsync(
            pending.Select(p => (p.Recipient, p.Request)).ToList());

        foreach ((PendingMail mail, EmailResult result) in pending.Zip(results))
        {
            if (result.Success)
                Log.Information($"CalendarMailBackgroundService: Sent '{mail.SettingType}' mail to {mail.Username}.");
            else
                Log.Error($"CalendarMailBackgroundService: Error while sending mail to {mail.Username}: {result.ErrorMessage}");
        }
    }

    private async Task<List<(string Username, string Email)>> ResolveRecipientsWithRetryAsync(CalendarMailJob job)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return await ResolveRecipientsAsync(job);
            }
            catch (DbException ex) when (attempt < 3)
            {
                Log.Warning(ex, $"CalendarMailBackgroundService: Retrying recipient lookup (attempt {attempt}).");
                await Task.Delay(TimeSpan.FromMilliseconds(300 * attempt));
            }
        }
    }

    private async Task<List<(string Username, string Email)>> ResolveRecipientsAsync(CalendarMailJob job)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        CryptoService crypto = scope.ServiceProvider.GetRequiredService<CryptoService>();

        MailSettingsEntity? mailSettings = await db.MailSettings.FirstOrDefaultAsync(x => x.DeletedAt == null);
        if (mailSettings == null)
        {
            Log.Warning("CalendarMailBackgroundService: No mail settings configured, skipping calendar mail.");
            return [];
        }

        List<Guid> optedOutUserIds = await db.UserSettings
            .Where(s => s.Type == job.SettingType && !s.Enabled && job.UserIds.Contains(s.UserId))
            .Select(s => s.UserId)
            .ToListAsync();

        List<UserEntity> users = await db.Users
            .Where(u => job.UserIds.Contains(u.Id) && !optedOutUserIds.Contains(u.Id))
            .ToListAsync();

        return users
            .Select(u => (u.Username, Email: crypto.Decrypt(u.Email) ?? string.Empty))
            .Where(r => !string.IsNullOrWhiteSpace(r.Email))
            .ToList();
    }

    private record PendingMail(string Username, UserSettingType SettingType, Recipient Recipient, EmailRequest Request);
}
