using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Data;
using UniVerein.Api.Data.Mail;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Interfaces;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using MailKit.Net.Smtp;
using Microsoft.EntityFrameworkCore;
using MailKit.Security;
using Microsoft.AspNetCore.SignalR;
using MimeKit;
using Serilog;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace UniVerein.Api.Services;

public class MailService
{
    private readonly AppDbContext _db;
    private readonly CryptoService _crypto;
    private readonly IHubContext<EmailProgressHub> _hubContext;
    private readonly IImapClientWrapper _imapClient;

    public MailService(AppDbContext db, CryptoService crypto, IHubContext<EmailProgressHub> hubContext,
        IImapClientWrapper? imapClient = null)
    {
        _db = db;
        _crypto = crypto;
        _hubContext = hubContext;
        _imapClient = imapClient ?? new ImapClientWrapper();
    }

    public async Task SendTestMailAsync(string email)
    {
        MailSettingsEntity? mailSettings = await _db.MailSettings
            .FirstOrDefaultAsync(x => x.DeletedAt == null);

        EmailRequest request = new()
        {
            Subject = $"Test mail from {mailSettings?.FromMail ?? ""}",
            HtmlBody = "Email successfully configured"
        };

        await SendEmailsAsync([new Recipient() { Email = email }], request, "123456789");
    }

    public async Task SendEmailWithBccOnlyAsync(List<Recipient> bccRecipients, EmailRequest request,
        string connectionId)
    {
        MailSettingsEntity? mailSettings = await _db.MailSettings.FirstOrDefaultAsync(x => x.DeletedAt == null);

        if (mailSettings is null)
            throw new NotFoundHttpException(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Mail settings not found.",
                moreInfo: "No mail settings could be found.");

        List<PreparedAttachment> preparedAttachments = request.Attachments.Select(att => new PreparedAttachment
        {
            FileName = att.FileName,
            ContentType = att.ContentType,
            ContentId = att.ContentId,
            IsInline = att.IsInline,
            Bytes = Convert.FromBase64String(att.Base64Content)
        }).ToList();

        MimeMessage message = BuildMessage(new Recipient()
        {
            Email = mailSettings.FromMail,
            FirstName = mailSettings.Username
        }, request, mailSettings, preparedAttachments);

        foreach (var bcc in bccRecipients)
            message.Bcc.Add(new MailboxAddress($"{bcc.FirstName} {bcc.LastName}", bcc.Email));

        await SendBccMessageAsync(message, mailSettings, connectionId);
    }

    private async Task SendBccMessageAsync(MimeMessage message, MailSettingsEntity settings, string connectionId)
    {
        using var smtpClient = new SmtpClient();

        try
        {
            await ConnectSmtpClientAsync(smtpClient, settings);
            EmailResult result = await SendOnExistingConnectionAsync(smtpClient, message,
                new Recipient() { Email = settings.FromMail, FirstName = settings.FromMail, LastName = string.Empty });
            await BccProgressUpdate(message, connectionId, result.Success);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SMTP connection failed for bcc mail");
            throw;
        }
        finally
        {
            await smtpClient.DisconnectAsync(true);
        }
    }

    private async Task BccProgressUpdate(MimeMessage message, string connectionId, bool success)
    {
        int totalCount = message.Bcc.Count;
        SendSummaryResult summary = new() { Total = totalCount };
        ProcessingState state = new();

        foreach (MailboxAddress address in message.Bcc.OfType<MailboxAddress>())
        {
            EmailResult result = new()
            {
                Email = address.Address,
                Success = success
            };

            (int currentProcessed, int successful, int failed) = state.RegisterResult(result);

            lock (summary)
            {
                summary.Results.Add(result);
                summary.Successful = successful;
                summary.Failed = failed;
            }

            await _hubContext.Clients.Client(connectionId).SendAsync("ProgressUpdate", new
            {
                progress = (int)((double)currentProcessed / totalCount * 100),
                processed = currentProcessed,
                total = totalCount,
                successful,
                failed,
                lastResult = result
            });
        }

        await _hubContext.Clients.Client(connectionId).SendAsync("SendComplete", summary);
    }

    public async Task SendEmailsAsync(List<Recipient> recipients, EmailRequest request, string connectionId)
    {
        SendSummaryResult summary = new() { Total = recipients.Count };
        MailSettingsEntity? mailSettings = await _db.MailSettings.FirstOrDefaultAsync(x => x.DeletedAt == null);

        if (mailSettings is null)
            throw new NotFoundHttpException(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Mail settings not found.",
                moreInfo: "No mail settings could be found.");

        List<PreparedAttachment> preparedAttachments = request.Attachments.Select(att => new PreparedAttachment
        {
            FileName = att.FileName,
            ContentType = att.ContentType,
            ContentId = att.ContentId,
            IsInline = att.IsInline,
            Bytes = Convert.FromBase64String(att.Base64Content)
        }).ToList();

        const int connectionCount = 3;

        var chunks = Enumerable.Range(0, connectionCount)
            .Select(i => recipients.Where((_, index) => index % connectionCount == i).ToList())
            .Where(chunk => chunk.Count > 0)
            .ToList();

        ProcessingState state = new();
        IEnumerable<Task> connectionTasks = chunks.Select(chunk =>
            ProcessChunkWithSingleConnectionAsync(
                chunk,
                request,
                mailSettings,
                preparedAttachments,
                summary,
                connectionId,
                state,
                recipients.Count
            )
        );

        MimeMessage message = BuildMessage(new Recipient()
        {
            Email = mailSettings.FromMail ?? string.Empty,
            FirstName = String.Join(", ", recipients.Select(x => x.Email).ToArray()),
            LastName = "",
        }, request, mailSettings, preparedAttachments);
        await CopyToSentFolderAsync(message, mailSettings);

        await Task.WhenAll(connectionTasks);
        await _hubContext.Clients.Client(connectionId).SendAsync("SendComplete", summary);
    }

    private async Task ProcessChunkWithSingleConnectionAsync(List<Recipient> chunk, EmailRequest request,
        MailSettingsEntity settings, List<PreparedAttachment> preparedAttachments,
        SendSummaryResult summary, string connectionId, ProcessingState state, int totalCount)
    {
        using SmtpClient client = new SmtpClient();

        try
        {
            await ConnectSmtpClientAsync(client, settings);

            foreach (Recipient recipient in chunk)
            {
                if (!client.IsConnected || !client.IsAuthenticated)
                {
                    Log.Warning("SMTP reconnecting for {Email}", recipient.Email);
                    await ConnectSmtpClientAsync(client, settings);
                }

                MimeMessage message = BuildMessage(recipient, request, settings, preparedAttachments);
                EmailResult result = await SendOnExistingConnectionAsync(client, message, recipient);
                (int currentProcessed, int successful, int failed) = state.RegisterResult(result);

                lock (summary)
                {
                    summary.Results.Add(result);
                    summary.Successful = successful;
                    summary.Failed = failed;
                }

                await _hubContext.Clients.Client(connectionId).SendAsync("ProgressUpdate", new
                {
                    progress = (int)((double)currentProcessed / totalCount * 100),
                    processed = currentProcessed,
                    total = totalCount,
                    successful,
                    failed,
                    lastResult = result
                });
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SMTP connection failed for chunk");
            throw;
        }
        finally
        {
            if (client.IsConnected)
                await client.DisconnectAsync(true);
        }
    }

    private async Task<EmailResult> SendOnExistingConnectionAsync(SmtpClient client, MimeMessage message,
        Recipient recipient)
    {
        try
        {
            await client.SendAsync(message);

            Log.Information("Sent to {Email}", recipient.Email);
            return new EmailResult { Email = recipient.Email, Success = true };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to send to {Email}", recipient.Email);
            return new EmailResult
            {
                Email = recipient.Email,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task CopyToSentFolderAsync(MimeMessage message, MailSettingsEntity settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ImapServer))
            return;

        try
        {
            SecureSocketOptions option = settings.EnableSsl ? SecureSocketOptions.Auto : SecureSocketOptions.None;

            await _imapClient.ConnectAsync(settings.ImapServer, settings.ImapPort, option);
            await _imapClient.AuthenticateAsync(settings.Username, _crypto.Decrypt(settings.Password) ?? string.Empty);
            await _imapClient.AppendAsync("Sent", message);
            await _imapClient.DisconnectAsync();

            Log.Information("Copy mail to sent-folder");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error while copy mail to Sent-folder");
        }
    }

    private async Task ConnectSmtpClientAsync(SmtpClient client, MailSettingsEntity settings)
    {
        try
        {
            if (client.IsConnected)
                await client.DisconnectAsync(true);

            SecureSocketOptions option = settings.EnableSsl ? SecureSocketOptions.Auto : SecureSocketOptions.None;
            await client.ConnectAsync(settings.SmtpServer, settings.Port, option);

            if (client.Capabilities.HasFlag(SmtpCapabilities.Authentication) &&
                !string.IsNullOrWhiteSpace(settings.Username))
                await client.AuthenticateAsync(settings.Username, _crypto.Decrypt(settings.Password) ?? string.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SMTP connection failed");
        }
    }

    private static MimeMessage BuildMessage(Recipient recipient, EmailRequest request, MailSettingsEntity settings,
        List<PreparedAttachment> preparedAttachments)
    {
        MimeMessage message = new();
        message.From.Add(new MailboxAddress(settings.FromMail, settings.FromMail));
        message.To.Add(new MailboxAddress($"{recipient.FirstName} {recipient.LastName}", recipient.Email));
        message.Subject = request.Subject;

        BodyBuilder bodyBuilder = new()
        {
            HtmlBody = request.HtmlBody.Replace("{fullname}", $"{recipient.FirstName} {recipient.LastName}")
                .Replace("{firstname}", recipient.FirstName)
        };

        foreach (PreparedAttachment att in preparedAttachments.Where(a => a.IsInline))
        {
            MimePart part = new MimePart(att.ContentType)
            {
                Content = new MimeContent(new MemoryStream(att.Bytes)),
                ContentId = att.ContentId,
                ContentDisposition = new ContentDisposition(ContentDisposition.Inline)
            };
            bodyBuilder.LinkedResources.Add(part);
        }

        foreach (var att in preparedAttachments.Where(a => !a.IsInline))
        {
            bodyBuilder.Attachments.Add(att.FileName, new MemoryStream(att.Bytes), ContentType.Parse(att.ContentType));
        }

        message.Body = bodyBuilder.ToMessageBody();
        return message;
    }
}