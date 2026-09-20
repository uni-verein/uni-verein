using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.Models;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Services;

public class SelfEnrollmentNotificationService
{
    private readonly AppDbContext _db;
    private readonly MailService _mailService;
    private readonly CryptoService _crypto;

    public SelfEnrollmentNotificationService(AppDbContext db, MailService mailService, CryptoService crypto)
    {
        _db = db;
        _mailService = mailService;
        _crypto = crypto;
    }

    public async Task NotifyAsync(PendingSelfEnrollmentEntity pending)
    {
        MailSettingsEntity? mailSettings = await _db.MailSettings.FirstOrDefaultAsync(x => x.DeletedAt == null);
        if (mailSettings == null)
        {
            Log.Warning("SelfEnrollmentNotificationService: No mail settings configured, skipping notification.");
            return;
        }

        List<UserEntity> recipients = await _db.UserSettings
            .Where(s => s.Type == UserSettingType.SELF_ENROLLMENT_NOTIFICATION && s.Enabled)
            .Select(s => s.User)
            .ToListAsync();

        if (recipients.Count == 0)
        {
            Log.Information("SelfEnrollmentNotificationService: No one subscribed, skipping notification.");
            return;
        }

        EmailRequest request = BuildEmailRequest(pending);

        foreach (UserEntity recipient in recipients)
        {
            string? email = _crypto.Decrypt(recipient.Email);
            if (string.IsNullOrWhiteSpace(email))
            {
                Log.Warning($"SelfEnrollmentNotificationService: User {recipient.Username} has no email, skipping.");
                continue;
            }

            try
            {
                await _mailService.SendEmailsAsync([new Recipient { Email = email }], request, "123456789");
                Log.Information(
                    $"SelfEnrollmentNotificationService: Notified {recipient.Username} about pending self-enrollment {pending.Id}.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"SelfEnrollmentNotificationService: Error while sending mail to {recipient.Username}");
            }
        }
    }

    private static EmailRequest BuildEmailRequest(PendingSelfEnrollmentEntity pending)
    {
        string body = $"""
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset="utf-8" />
                    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                    <title>Neue Selbstregistrierung</title>
                </head>
                <body style="font-family: Arial, sans-serif; line-height: 1.6; color: #333;">
                    <h2>🧑‍🎓 Neue Selbstregistrierung / New self-enrollment</h2>

                    <hr/>
                    <h3>🇩🇪 Deutsch</h3>
                    <p><strong>{pending.FirstName} {pending.LastName}</strong> hat sich selbst registriert und die
                    E-Mail-Adresse bestätigt.</p>
                    <p>Bitte prüfe die Anmeldung im Tab "Ausstehende Mitglieder" der Mitgliederverwaltung.</p>

                    <hr/>
                    <h3>🇬🇧 English</h3>
                    <p><strong>{pending.FirstName} {pending.LastName}</strong> self-enrolled and confirmed their
                    email address.</p>
                    <p>Please review the registration in the "Pending members" tab of the member management page.</p>
                </body>
                </html>
                """;

        return new EmailRequest
        {
            Subject = "🧑‍🎓 Neue Selbstregistrierung / New self-enrollment",
            HtmlBody = body,
            Attachments = []
        };
    }
}
