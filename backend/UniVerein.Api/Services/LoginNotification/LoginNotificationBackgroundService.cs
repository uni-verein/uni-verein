using System;
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
using UniVerein.Api.Tenancy;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using System.Collections.Generic;

namespace UniVerein.Api.Services.LoginNotification;

public class LoginNotificationBackgroundService : BackgroundService
{
    private readonly LoginNotificationQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;

    public LoginNotificationBackgroundService(LoginNotificationQueue queue, IServiceScopeFactory scopeFactory)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            LoginNotificationJob job;
            try
            {
                job = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                await SendAsync(job);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "LoginNotificationBackgroundService: Error while sending the new device mail to user {UserId}.", job.UserId);
            }
        }
    }

    private async Task SendAsync(LoginNotificationJob job)
    {
        using IServiceScope scope = _scopeFactory.CreateTenantScope(job.TenantId);
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        CryptoService crypto = scope.ServiceProvider.GetRequiredService<CryptoService>();
        MailService mailService = scope.ServiceProvider.GetRequiredService<MailService>();

        MailSettingsEntity? mailSettings = await db.MailSettings.FirstOrDefaultAsync(x => x.DeletedAt == null);
        if (mailSettings == null)
        {
            Log.Warning("LoginNotificationBackgroundService: No mail settings configured, skipping new device mail.");
            return;
        }

        UserEntity? user = await db.Users.FirstOrDefaultAsync(u => u.Id == job.UserId);
        string email = crypto.Decrypt(user?.Email) ?? string.Empty;
        if (user == null || string.IsNullOrWhiteSpace(email))
        {
            Log.Information("LoginNotificationBackgroundService: User {UserId} has no email address, skipping new device mail.", job.UserId);
            return;
        }

        string? baseUrl = await db.WebPageConfigs.Select(c => c.PublicBaseUrl).FirstOrDefaultAsync();
        string? link = string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl.TrimEnd('/');

        EmailRequest request = LoginNotificationMailBuilder.Build(user.Username, job, link);
        List<EmailResult> results = await mailService.SendBatchAsync([(new Recipient { Email = email }, request)]);

        if (results.Count == 1 && results[0].Success)
            Log.Information("LoginNotificationBackgroundService: Sent new device mail to user {UserId}.", job.UserId);
        else
            Log.Error("LoginNotificationBackgroundService: Error while sending new device mail to user {UserId}.", job.UserId);
    }
}
