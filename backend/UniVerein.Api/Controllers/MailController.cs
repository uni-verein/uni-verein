using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Data;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Query;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniVerein.Api.Services;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace UniVerein.Api.Controllers;

[Authorize]
[ApiController]
[Route("mail")]
public class MailController : ControllerBase
{
    private const int MAX_PERSONAL_MAIL_COUNT = 150;
    private readonly AppDbContext _db;
    private readonly MailService _mail;
    private readonly CryptoService _crypto;

    public MailController(AppDbContext db, MailService mail, CryptoService crypto)
    {
        _db = db;
        _mail = mail;
        _crypto = crypto;
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpPost("test")]
    public async Task<IActionResult> SendTestAsync([FromBody] TestMailRequest request)
    {
        Log.Information(
            $"MailController: SendTestAsync -> Try to send test mail. Request: {JsonSerializer.Serialize(request)}");

        await _mail.SendTestMailAsync(request.Email);

        Log.Information("MailController: SendTestAsync -> Test mail successfully sent.");
        return Ok("Test mail successfully sent.");
    }

    [HttpGet("recipients")]
    public async Task<ActionResult<AllRecipientResult>> GetRecipients([FromQuery] RecipientQuery recipientQuery)
    {
        if (recipientQuery.Offset < 0 || recipientQuery.Limit < 1)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Offset and/or Limit must be greater than or equal to 1."));

        IQueryable<MemberEntity> memberQuery = _db.Members.AsQueryable()
            .Where(x => x.DeletedAt == null && x.BulkMail != BulkMail.NOT_ALLOWED);

        if (recipientQuery.CategoryId != null && recipientQuery.CategoryId != Guid.Parse(Program.MemberCategoriesAll))
            memberQuery = memberQuery.Where(x => x.MemberCategoryId == recipientQuery.CategoryId);

        int total = await memberQuery.CountAsync();

        List<MemberEntity> recipients = await memberQuery.OrderBy(x => x.FirstName).Skip(recipientQuery.Offset)
            .Take(recipientQuery.Limit).ToListAsync();

        AllRecipientResult result = new AllRecipientResult()
        {
            Total = total,
            Items = recipients.Select(x => new RecipientResult()
            {
                Email = _crypto.Decrypt(x.EmailEncrypted) ?? string.Empty,
                FirstName = x.FirstName,
                LastName = x.LastName
            }).ToList()
        };

        return Ok(result);
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendAsync([FromBody] MailSendRequest request)
    {
        Log.Information(
            $"MailController: SendAsync -> Try to send mail. Request: {{Subject: {request.EmailData.Subject}, Body lenght: {request.EmailData.HtmlBody.Length}, Attachments count: {request.EmailData.Attachments.Count} }}");

        if (string.IsNullOrEmpty(request.ConnectionId))
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "ConnectionId missing"));

        var allRecipients = await _db.Members.Include(x => x.MemberCategory)
            .Where(x => x.DeletedAt == null && x.BulkMail != BulkMail.NOT_ALLOWED).Select(x => new
            {
                Email = _crypto.Decrypt(x.EmailEncrypted),
                x.FirstName,
                x.LastName,
                x.MemberCategoryId
            }).ToListAsync();

        List<Recipient> recipients = new();
        if (request.CategoryId == null && request.SelectedEmails?.Any() == true)
        {
            recipients = allRecipients
                .Where(r => request.SelectedEmails.Contains(r.Email ?? string.Empty))
                .Select(x => new Recipient()
                {
                    Email = x.Email ?? string.Empty,
                    FirstName = x.FirstName,
                    LastName = x.LastName
                }).ToList();
        }
        else if (request.CategoryId != null)
        {
            recipients = allRecipients
                .Where(x => request.CategoryId == Guid.Parse(Program.MemberCategoriesAll) ||
                            x.MemberCategoryId == request.CategoryId)
                .Select(x => new Recipient()
                {
                    Email = x.Email ?? string.Empty,
                    FirstName = x.FirstName,
                    LastName = x.LastName
                }).ToList();
        }

        if (!recipients.Any())
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Emails could not be sent.",
                moreInfo: "Emails cannot be sent because no email was selected."));

        if (recipients.Count > MAX_PERSONAL_MAIL_COUNT)
        {
            if (request.EmailData.HtmlBody.Contains("{firstname}") || request.EmailData.HtmlBody.Contains("{fullname}"))
                return BadRequest(new ApiResults.ErrorResults.BadRequestResult(errorMessage: "Message contains personalized content",
                    moreInfo: "The message must not contain personalized content: {firstname} or {fullname}. " +
                              "Personalized content is only allowed by recipient count less than 151."));
            await _mail.SendEmailWithBccOnlyAsync(recipients, request.EmailData, request.ConnectionId);
        }
        else
        {
            await _mail.SendEmailsAsync(recipients, request.EmailData, request.ConnectionId);
        }

        return Ok(new { message = "Versand gestartet", total = recipients.Count });
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpGet]
    public async Task<ActionResult<MailSettingsResult>> GetAsync()
    {
        MailSettingsEntity? mailSetting = await _db.MailSettings.FirstOrDefaultAsync(x => x.DeletedAt == null);
        if (mailSetting == null)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Mail settings not found.",
                moreInfo: "No mail settings could be found."));

        MailSettingsResult result = new()
        {
            Id = mailSetting.Id,
            SmtpServer = mailSetting.SmtpServer,
            Port = mailSetting.Port,
            ImapServer = mailSetting.ImapServer ?? string.Empty,
            ImapPort = mailSetting.ImapPort,
            Username = mailSetting.Username,
            Password = "",
            FromMail = mailSetting.FromMail,
            EnableSsl = mailSetting.EnableSsl
        };

        return Ok(result);
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpPut]
    public async Task<ActionResult<MailSettingsResult>> UpdateAsync([FromBody] MailSettingsRequest request)
    {
        Log.Information(
            $"MailController: UpdateAsync -> Try to update mail settings. SmtpServer: {request.SmtpServer}");

        MailSettingsEntity? mailSetting = await _db.MailSettings.FirstOrDefaultAsync();
        if (mailSetting == null)
        {
            mailSetting = new()
            {
                SmtpServer = request.SmtpServer.Trim(),
                Port = request.Port,
                ImapServer = request.ImapServer.Trim(),
                ImapPort = request.ImapPort,
                Username = request.Username.Trim(),
                Password = _crypto.Encrypt(request.Password),
                FromMail = request.FromMail.Trim(),
                EnableSsl = request.EnableSsl ?? true
            };
            await _db.MailSettings.AddAsync(mailSetting);
        }
        else
        {
            mailSetting.SmtpServer = request.SmtpServer.Trim();
            mailSetting.Port = request.Port;
            mailSetting.ImapServer = request.ImapServer.Trim();
            mailSetting.ImapPort = request.ImapPort;
            mailSetting.Username = request.Username.Trim();
            if (!string.IsNullOrWhiteSpace(request.Password))
                mailSetting.Password = _crypto.Encrypt(request.Password);
            mailSetting.FromMail = request.FromMail.Trim();
            mailSetting.EnableSsl = request.EnableSsl ?? true;
            mailSetting.DeletedAt = null;
            _db.MailSettings.Update(mailSetting);
        }

        await _db.SaveChangesAsync();

        Log.Information($"MailController: UpdateAsync -> Mail settings successfully updated for ID: {mailSetting.Id}.");
        return Ok(new MailSettingsResult()
        {
            Id = mailSetting.Id,
            SmtpServer = mailSetting.SmtpServer,
            Port = mailSetting.Port,
            ImapServer = mailSetting.ImapServer,
            ImapPort = mailSetting.ImapPort,
            Username = mailSetting.Username,
            Password = "",
            FromMail = mailSetting.FromMail,
            EnableSsl = mailSetting.EnableSsl
        });
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] Guid id)
    {
        MailSettingsEntity? mailSetting = await _db.MailSettings.FindAsync(id);
        if (mailSetting == null)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Mail setting with the given ID not found.",
                moreInfo: $"No mail settings with the ID {id} could be found."));

        _db.Remove(mailSetting);
        await _db.SaveChangesAsync();
        return Ok();
    }
}