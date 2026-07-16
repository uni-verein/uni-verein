using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serilog;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.Helper;
using UniVerein.Api.Models;
using UniVerein.Api.Models.Firmware;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Services.Firmware;

public class FirmwareService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _dbContext;
    private readonly MailService _mailService;
    private readonly CryptoService _crypto;
    

    public FirmwareService(HttpClient httpClient, IConfiguration configuration, AppDbContext db, MailService mail, CryptoService crypto)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _dbContext = db;
        _mailService = mail;
        _crypto = crypto;
        
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("UniVerein-App");
        }
    }

    public async Task CheckLatestFirmwareAsync(CancellationToken cancellationToken)
    {
        var currentVersionRaw = _configuration["Version"];
        
        if (string.IsNullOrWhiteSpace(currentVersionRaw)) 
        { 
            Log.Warning("FirmwareService: No firmware version (ENV Version) configured."); 
            return;
        }
        
        GithubReleaseResponse? release = await _httpClient.GetFromJsonAsync<GithubReleaseResponse>("https://api.github.com/repos/uni-verein/uni-verein/releases/latest", cancellationToken);
        if (release == null)
            return;
        
        if (!Version.TryParse(currentVersionRaw, out var currentVersion)) 
        { 
            Log.Error($"FirmwareService: Current version '{currentVersionRaw}' could not be parsed."); 
            return;
        }
        
        if (!Version.TryParse(release.Name?.TrimStart('v'), out var latestVersion)) 
        { 
            Log.Error($"FirmwareService: GitHub version '{release.Name}' could not be parsed."); 
            return;
        }
        
        if (latestVersion <= currentVersion)
            return;
        
        FirmwareVersionEntity? existingEntry = await _dbContext.FirmwareVersions.FirstOrDefaultAsync(f => f.Version == release.Name, cancellationToken);
        FirmwareVersionEntity firmwareVersion;
        
        if (existingEntry == null) 
        { 
            firmwareVersion = new() 
            { 
                Version = release.Name!,
                TagName = release.TagName!,
                ReleaseNotes = release.Body,
                PublishedAt = release.PublishedAt ?? DateTimeOffset.UtcNow
            };
            
            await _dbContext.FirmwareVersions.AddAsync(firmwareVersion, cancellationToken); 
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else 
        { 
            firmwareVersion = existingEntry;
        }
        
        MailSettingsEntity? mailSettings = await _dbContext.MailSettings.FirstOrDefaultAsync(x => x.DeletedAt == null);
        if (mailSettings == null)
            return;
        
        List<Guid> alreadyNotifiedUserIds = await _dbContext.FirmwareVersionNotifications
            .Where(n => n.FirmwareVersionId == firmwareVersion.Id)
            .Select(n => n.UserId)
            .ToListAsync(cancellationToken);

        List<UserEntity> adminsToNotify = await _dbContext.Users.Where(u => u.Role == UserRole.ADMIN && !alreadyNotifiedUserIds.Contains(u.Id)).ToListAsync(cancellationToken);

        if (!adminsToNotify.Any()) 
            return;
        
        EmailRequest request = BuildEmailRequest(firmwareVersion);
        
        foreach (UserEntity admin in adminsToNotify)
        {
            string? email = _crypto.Decrypt(admin.Email);
            if (string.IsNullOrWhiteSpace(email))
                return;
            
            try
            {
                await _mailService.SendEmailsAsync([new Recipient() { Email = email }], request, "123456789");

                _dbContext.FirmwareVersionNotifications.Add(new FirmwareVersionNotificationEntity() 
                { 
                    FirmwareVersionId = firmwareVersion.Id,
                    FirmwareVersion = firmwareVersion,
                    UserId = admin.Id,
                    User = admin
                });
                
                Log.Information($"FirmwareService: Admin {admin.Username} notified about version {firmwareVersion.Version}.");
            }
            catch (Exception ex) 
            { 
                Log.Error(ex, $"FirmwareService: Error while sending mail to {admin.Username}");
            }
        }
        
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
    
    private static EmailRequest BuildEmailRequest(FirmwareVersionEntity firmware)
    {
        var releaseNotesHtml = MarkdownHelper.ToHtml(firmware.ReleaseNotes);

        string body = $"""
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset="utf-8" />
                    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                    <title>Uni-Verein {firmware.Version}</title>
                </head>
                <body style="font-family: Arial, sans-serif; line-height: 1.6; color: #333;">
                    <h2>🚀 Neue Version von Uni-Verein verfügbar / New Uni-Verein version available: {firmware.Version}</h2>

                    <hr/>
                    <h3>🇩🇪 Deutsch</h3>
                    <p>Es ist eine neue Version der Software <strong>Uni-Verein</strong> verfügbar.</p>
                    <p><strong>Veröffentlicht am:</strong> {firmware.PublishedAt:g}</p>
                    <p><strong>Änderungen:</strong></p>
                    <div style="background:#f6f8fa; padding: 16px; border-radius: 6px;">
                        {releaseNotesHtml}
                    </div>

                    <hr/>
                    <h3>🇬🇧 English</h3>
                    <p>A new version of the <strong>Uni-Verein</strong> software is available.</p>
                    <p><strong>Published at:</strong> {firmware.PublishedAt:g}</p>
                    <p><strong>Changes:</strong></p>
                    <div style="background:#f6f8fa; padding: 16px; border-radius: 6px;">
                        {releaseNotesHtml}
                    </div>
                </body>
                </html>
                """;
        
        return new()
        {
            Subject = $"🚀 New Uni-Verein version available: {firmware.Version}",
            HtmlBody = body,
            Attachments = []
        };
    }
}