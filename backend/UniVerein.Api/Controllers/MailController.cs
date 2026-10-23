using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Models;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Extensions;
using UniVerein.Api.Helper;
using UniVerein.Api.Query;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniVerein.Api.Models.Enums;
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
    private readonly AuditService _auditService;

    public MailController(AppDbContext db, MailService mail, CryptoService crypto, AuditService auditService)
    {
        _db = db;
        _mail = mail;
        _crypto = crypto;
        _auditService = auditService;
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpPost("test")]
    public async Task<ActionResult<string>> SendTestAsync([FromBody] TestMailRequest request)
    {
        Log.Information("MailController: SendTestAsync -> Try to send test mail, requested by {ActorId}.", HttpContext.GetActorId());

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

        int total;
        List<MemberEntity> recipients;

        if (!string.IsNullOrWhiteSpace(recipientQuery.Name))
        {
            List<MemberEntity> members = await memberQuery.ToListAsync();
            recipients = members
                .Select(x => new
                {
                    member = x,
                    score = NameSearchHelper.Score(recipientQuery.Name,
                        $"{x.FirstName} {x.MiddleName} {x.LastName}", x.FirstName, x.MiddleName, x.LastName)
                })
                .Where(x => NameSearchHelper.IsMatch(x.score))
                .OrderByDescending(x => x.score)
                .Select(x => x.member)
                .ToList();
            total = recipients.Count;
            recipients = recipients.Skip(recipientQuery.Offset).Take(recipientQuery.Limit).ToList();
        }
        else
        {
            total = await memberQuery.CountAsync();
            recipients = await memberQuery.OrderBy(x => x.FirstName).Skip(recipientQuery.Offset)
                .Take(recipientQuery.Limit).ToListAsync();
        }

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
        Log.Information("MailController: SendAsync -> Try to send mail by {ActorId}. Body length: {BodyLength}, attachments: {AttachmentCount}.",
            HttpContext.GetActorId(), request.EmailData.HtmlBody.Length, request.EmailData.Attachments.Count);

        if (string.IsNullOrEmpty(request.ConnectionId))
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "ConnectionId missing"));

        request.EmailData.HtmlBody = MailHtmlSanitizer.Sanitize(request.EmailData.HtmlBody);

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
        {
            Log.Warning("MailController: SendAsync -> No recipients selected, nothing sent.");
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Emails could not be sent.",
                moreInfo: "Emails cannot be sent because no email was selected."));
        }

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

        Log.Information("MailController: SendAsync -> Mail dispatched by {ActorId} to {RecipientCount} recipients (bcc only: {BccOnly}).",
            HttpContext.GetActorId(), recipients.Count, recipients.Count > MAX_PERSONAL_MAIL_COUNT);
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
            FromName = mailSetting.FromName,
            EnableSsl = mailSetting.EnableSsl
        };

        return Ok(result);
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpPut]
    public async Task<ActionResult<MailSettingsResult>> UpdateAsync([FromBody] MailSettingsRequest request)
    {
        Log.Information("MailController: UpdateAsync -> Try to update mail settings by {ActorId}. SmtpServer: {SmtpServer}",
            HttpContext.GetActorId(), request.SmtpServer);

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
                FromName = request.FromName?.Trim() ?? string.Empty,
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
            mailSetting.FromName = request.FromName?.Trim() ?? string.Empty;
            mailSetting.EnableSsl = request.EnableSsl ?? true;
            mailSetting.DeletedAt = null;
            _db.MailSettings.Update(mailSetting);
        }

        await _db.SaveChangesAsync();
        await _auditService.LogAsync(AuditLogActions.UPDATE, nameof(MailSettingsEntity), new
        {
            MailSettingsId = mailSetting.Id,
            mailSetting.SmtpServer,
            mailSetting.Port,
            mailSetting.ImapServer,
            mailSetting.ImapPort,
            mailSetting.Username,
            mailSetting.FromMail,
            mailSetting.FromName,
            mailSetting.EnableSsl,
            PasswordChanged = !string.IsNullOrWhiteSpace(request.Password)
        });

        Log.Information("MailController: UpdateAsync -> Mail settings {MailSettingsId} updated by {ActorId}.",
            mailSetting.Id, HttpContext.GetActorId());
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
            FromName = mailSetting.FromName,
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
        await _auditService.LogAsync(AuditLogActions.SOFT_DELETE, nameof(MailSettingsEntity),
            new { MailSettingsId = id, mailSetting.SmtpServer });
        Log.Information("MailController: DeleteAsync -> Mail settings {MailSettingsId} deleted by {ActorId}.", id, HttpContext.GetActorId());
        return Ok();
    }
}