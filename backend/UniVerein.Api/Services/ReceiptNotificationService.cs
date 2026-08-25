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

public class ReceiptNotificationService
{
    private readonly AppDbContext _db;
    private readonly MailService _mailService;
    private readonly CryptoService _crypto;

    public ReceiptNotificationService(AppDbContext db, MailService mailService, CryptoService crypto)
    {
        _db = db;
        _mailService = mailService;
        _crypto = crypto;
    }

    public async Task NotifyFinancialManagersAsync(ReceiptEntity receipt, UserEntity creator)
    {
        if (creator.Role == UserRole.FINANCIAL_MANAGER)
            return;

        MailSettingsEntity? mailSettings = await _db.MailSettings.FirstOrDefaultAsync(x => x.DeletedAt == null);
        if (mailSettings == null)
        {
            Log.Warning("ReceiptNotificationService: No mail settings configured, skipping receipt notification.");
            return;
        }

        List<Guid> optedOutUserIds = await _db.UserSettings
            .Where(s => s.Type == UserSettingType.RECEIPT_NOTIFICATION && !s.Enabled)
            .Select(s => s.UserId)
            .ToListAsync();

        List<UserEntity> recipients = await _db.Users
            .Where(u => u.Role == UserRole.FINANCIAL_MANAGER && !optedOutUserIds.Contains(u.Id))
            .ToListAsync();

        if (recipients.Count == 0)
            return;

        EmailRequest request = BuildEmailRequest(receipt, creator);

        foreach (UserEntity recipient in recipients)
        {
            string? email = _crypto.Decrypt(recipient.Email);
            if (string.IsNullOrWhiteSpace(email))
            {
                Log.Warning($"ReceiptNotificationService: User {recipient.Username} has no email, skipping.");
                continue;
            }

            try
            {
                await _mailService.SendEmailsAsync([new Recipient { Email = email }], request, "123456789");
                Log.Information($"ReceiptNotificationService: Notified {recipient.Username} about receipt {receipt.Id}.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"ReceiptNotificationService: Error while sending mail to {recipient.Username}");
            }
        }
    }

    private static EmailRequest BuildEmailRequest(ReceiptEntity receipt, UserEntity creator)
    {
        string body = $"""
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset="utf-8" />
                    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                    <title>Neuer Beleg eingereicht</title>
                </head>
                <body style="font-family: Arial, sans-serif; line-height: 1.6; color: #333;">
                    <h2>🧾 Neuer Beleg eingereicht / New receipt submitted</h2>

                    <hr/>
                    <h3>🇩🇪 Deutsch</h3>
                    <p>Ein neuer Beleg wurde von <strong>{creator.Username}</strong> eingereicht.</p>
                    <p><strong>Betrag:</strong> {receipt.Amount:0.00} €</p>
                    <p><strong>Belegdatum:</strong> {receipt.ReceiptDate:d}</p>
                    <p>Bitte prüfen Sie den Beleg in der Belegsverwaltung von Uni-Verein.</p>

                    <hr/>
                    <h3>🇬🇧 English</h3>
                    <p>A new receipt was submitted by <strong>{creator.Username}</strong>.</p>
                    <p><strong>Amount:</strong> {receipt.Amount:0.00} €</p>
                    <p><strong>Receipt date:</strong> {receipt.ReceiptDate:d}</p>
                    <p>Please review the receipt in Uni-Verein's receipt management.</p>
                </body>
                </html>
                """;

        return new EmailRequest
        {
            Subject = "🧾 Neuer Beleg eingereicht / New receipt submitted",
            HtmlBody = body,
            Attachments = []
        };
    }
}
