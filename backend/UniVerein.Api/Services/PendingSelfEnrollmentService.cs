using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.Models;
using UniVerein.Api.Models.Enums;
using UniVerein.Api.Models.MemberAudit;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Services;

public class PendingSelfEnrollmentService
{
    private static readonly TimeSpan ConfirmationTokenLifetime = TimeSpan.FromHours(48);

    private readonly AppDbContext _db;
    private readonly CryptoService _crypto;
    private readonly MemberService _memberService;
    private readonly MailService _mailService;
    private readonly SelfEnrollmentNotificationService _notificationService;
    private readonly AuditService _auditService;
    private readonly TimeProvider _timeProvider;

    public PendingSelfEnrollmentService(AppDbContext db, CryptoService crypto, MemberService memberService,
        MailService mailService, SelfEnrollmentNotificationService notificationService, AuditService auditService,
        TimeProvider timeProvider)
    {
        _db = db;
        _crypto = crypto;
        _memberService = memberService;
        _mailService = mailService;
        _notificationService = notificationService;
        _auditService = auditService;
        _timeProvider = timeProvider;
    }

    // confirmBaseUrl: the public frontend URL the confirmation link should point to (e.g. "https://club.example/enroll/confirm").
    public async Task<PendingSelfEnrollmentSubmitResult> SubmitAsync(SelfEnrollmentRequest request, string submittedIp,
        string confirmBaseUrl)
    {
        string emailHash = _crypto.Hash(request.Email);
        string ibanHash = _crypto.Hash(request.IBAN);
        bool ibanGiven = !string.IsNullOrWhiteSpace(request.IBAN);

        bool memberExists = await _db.Members
            .AnyAsync(x => x.EmailHash == emailHash || (ibanGiven && x.IBAN_Hash == ibanHash));
        if (memberExists)
            return PendingSelfEnrollmentSubmitResult.Failure(MemberCreationStatus.DUPLICATE_CONFLICT);

        bool pendingExists = await _db.PendingSelfEnrollments.AnyAsync(x =>
            (x.Status == PendingSelfEnrollmentStatus.AWAITING_EMAIL_CONFIRMATION ||
             x.Status == PendingSelfEnrollmentStatus.PENDING) &&
            (x.EmailHash == emailHash || (ibanGiven && x.IBAN_Hash == ibanHash)));
        if (pendingExists)
            return PendingSelfEnrollmentSubmitResult.Failure(MemberCreationStatus.DUPLICATE_CONFLICT);

        string token = GenerateToken();
        DateTimeOffset now = _timeProvider.GetUtcNow();

        PendingSelfEnrollmentEntity pending = new()
        {
            Status = PendingSelfEnrollmentStatus.AWAITING_EMAIL_CONFIRMATION,
            ConfirmationTokenHash = _crypto.Hash(token),
            ConfirmationTokenExpiresAt = now.Add(ConfirmationTokenLifetime),
            SubmittedIp = submittedIp,
            Gender = request.Gender,
            FirstName = request.FirstName,
            MiddleName = request.MiddleName,
            LastName = request.LastName,
            BirthdayEncrypted = _crypto.Encrypt(request.Birthday),
            StreetEncrypted = _crypto.Encrypt(request.Street),
            PostalCode = request.PostalCode,
            City = request.City,
            CountryCode = request.CountryCode,
            EmailEncrypted = _crypto.Encrypt(request.Email),
            EmailHash = emailHash,
            PhoneEncrypted = _crypto.Encrypt(request.Phone),
            BulkMail = request.BulkMail,
            StartOfStudies = request.StartOfStudies,
            EndOfStudies = request.EndOfStudies,
            AcademicDegree = request.AcademicDegree,
            CourseOfStudy = request.CourseOfStudy,
            MotivationEncrypted = _crypto.Encrypt(request.Motivation),
            MemberCategoryId = null,
            MemberCategory = null,
            IBAN_Encrypted = _crypto.Encrypt(request.IBAN),
            IBAN_Hash = ibanHash,
            Bic_Encrypted = _crypto.Encrypt(request.Bic),
            SepaConsent = request.SepaConsent,
            ContributionPlanId = null,
            ContributionPlan = null
        };

        await _db.PendingSelfEnrollments.AddAsync(pending);
        await _db.SaveChangesAsync();

        await SendConfirmationMailAsync(request, token, confirmBaseUrl);

        return PendingSelfEnrollmentSubmitResult.Success(pending);
    }

    public async Task<PendingSelfEnrollmentActionStatus> ConfirmAsync(string token)
    {
        string tokenHash = _crypto.Hash(token);
        DateTimeOffset now = _timeProvider.GetUtcNow();

        PendingSelfEnrollmentEntity? pending = await _db.PendingSelfEnrollments.FirstOrDefaultAsync(x =>
            x.Status == PendingSelfEnrollmentStatus.AWAITING_EMAIL_CONFIRMATION &&
            x.ConfirmationTokenHash == tokenHash &&
            x.ConfirmationTokenExpiresAt > now);

        if (pending == null)
            return PendingSelfEnrollmentActionStatus.NOT_FOUND;

        pending.Status = PendingSelfEnrollmentStatus.PENDING;
        pending.ConfirmedAt = now;
        _db.Update(pending);
        await _db.SaveChangesAsync();

        try
        {
            await _notificationService.NotifyAsync(pending);
        }
        catch (Exception ex)
        {
            Log.Error(ex,
                $"PendingSelfEnrollmentService: Error while notifying subscribers about pending self-enrollment {pending.Id}");
        }

        return PendingSelfEnrollmentActionStatus.SUCCESS;
    }

    public async Task<PendingSelfEnrollmentApprovalResult> ApproveAsync(Guid id)
    {
        PendingSelfEnrollmentEntity? pending = await _db.PendingSelfEnrollments
            .FirstOrDefaultAsync(x => x.Id == id && x.Status == PendingSelfEnrollmentStatus.PENDING);
        if (pending == null)
            return new PendingSelfEnrollmentApprovalResult(PendingSelfEnrollmentActionStatus.NOT_FOUND);

        if (pending.MemberCategoryId == null)
            return new PendingSelfEnrollmentApprovalResult(PendingSelfEnrollmentActionStatus.MEMBER_CATEGORY_NOT_SET);

        MemberCreationResult result = await _memberService.CreateMemberAsync(new MemberCreationInput
        {
            Gender = pending.Gender,
            FirstName = pending.FirstName,
            MiddleName = pending.MiddleName,
            LastName = pending.LastName,
            Birthday = _crypto.DecryptDate(pending.BirthdayEncrypted) ?? DateTimeOffset.MinValue,
            Street = _crypto.Decrypt(pending.StreetEncrypted) ?? string.Empty,
            PostalCode = pending.PostalCode,
            City = pending.City,
            CountryCode = pending.CountryCode ?? string.Empty,
            Email = _crypto.Decrypt(pending.EmailEncrypted) ?? string.Empty,
            Phone = _crypto.Decrypt(pending.PhoneEncrypted) ?? string.Empty,
            BulkMail = pending.BulkMail,
            StartOfStudies = pending.StartOfStudies,
            EndOfStudies = pending.EndOfStudies,
            AcademicDegree = pending.AcademicDegree,
            CourseOfStudy = pending.CourseOfStudy,
            TaskWithinTheClub = TaskWithinTheClub.MEMBER,
            MemberCategoryId = pending.MemberCategoryId.Value,
            IBAN = _crypto.Decrypt(pending.IBAN_Encrypted) ?? string.Empty,
            Bic = _crypto.Decrypt(pending.Bic_Encrypted) ?? string.Empty,
            SepaConsent = pending.SepaConsent,
            EntryDate = _timeProvider.GetUtcNow(),
            ExitDate = null,
            ContributionPlanId = pending.ContributionPlanId
        });

        switch (result.Status)
        {
            case MemberCreationStatus.DUPLICATE_CONFLICT:
                return new PendingSelfEnrollmentApprovalResult(PendingSelfEnrollmentActionStatus.DUPLICATE_CONFLICT);
            case MemberCreationStatus.CONTRIBUTION_PLAN_NOT_FOUND:
                return new PendingSelfEnrollmentApprovalResult(PendingSelfEnrollmentActionStatus.CONTRIBUTION_PLAN_NOT_FOUND);
            case MemberCreationStatus.MEMBER_CATEGORY_NOT_FOUND:
                return new PendingSelfEnrollmentApprovalResult(PendingSelfEnrollmentActionStatus.MEMBER_CATEGORY_NOT_FOUND);
        }

        _db.PendingSelfEnrollments.Remove(pending);
        await _db.ForceSaveChangesAsync();

        await SendAcceptanceMailAsync(pending);

        return new PendingSelfEnrollmentApprovalResult(PendingSelfEnrollmentActionStatus.SUCCESS, result.Member);
    }

    public async Task<PendingSelfEnrollmentActionStatus> RejectAsync(Guid id, string? reason)
    {
        PendingSelfEnrollmentEntity? pending = await _db.PendingSelfEnrollments
            .FirstOrDefaultAsync(x => x.Id == id && x.Status == PendingSelfEnrollmentStatus.PENDING);
        if (pending == null)
            return PendingSelfEnrollmentActionStatus.NOT_FOUND;

        await _auditService.LogAsync(AuditLogActions.DELETE, nameof(PendingSelfEnrollmentEntity), new
        {
            PendingSelfEnrollmentId = pending.Id,
            pending.FirstName,
            pending.LastName,
            Reason = reason
        });

        _db.PendingSelfEnrollments.Remove(pending);
        await _db.ForceSaveChangesAsync();

        return PendingSelfEnrollmentActionStatus.SUCCESS;
    }

    public async Task<PendingSelfEnrollmentEntity?> GetByIdAsync(Guid id)
    {
        return await _db.PendingSelfEnrollments
            .Include(x => x.MemberCategory)
            .Include(x => x.ContributionPlan)
            .FirstOrDefaultAsync(x => x.Id == id && x.Status == PendingSelfEnrollmentStatus.PENDING);
    }

    public async Task<PendingSelfEnrollmentUpdateResult> UpdateAsync(Guid id, PendingSelfEnrollmentUpdateRequest request)
    {
        PendingSelfEnrollmentEntity? pending = await _db.PendingSelfEnrollments
            .Include(x => x.MemberCategory)
            .Include(x => x.ContributionPlan)
            .FirstOrDefaultAsync(x => x.Id == id && x.Status == PendingSelfEnrollmentStatus.PENDING);
        if (pending == null)
            return new PendingSelfEnrollmentUpdateResult(PendingSelfEnrollmentActionStatus.NOT_FOUND);

        if (request.Email != null || request.IBAN != null)
        {
            string emailHash = request.Email != null ? _crypto.Hash(request.Email) : pending.EmailHash;
            string? ibanHash = request.IBAN != null ? _crypto.Hash(request.IBAN) : pending.IBAN_Hash;
            bool ibanGiven = !string.IsNullOrWhiteSpace(request.IBAN != null ? request.IBAN : _crypto.Decrypt(pending.IBAN_Encrypted));

            bool memberExists = await _db.Members
                .AnyAsync(x => x.EmailHash == emailHash || (ibanGiven && x.IBAN_Hash == ibanHash));
            if (memberExists)
                return new PendingSelfEnrollmentUpdateResult(PendingSelfEnrollmentActionStatus.DUPLICATE_CONFLICT);

            bool pendingExists = await _db.PendingSelfEnrollments.AnyAsync(x =>
                x.Id != id &&
                (x.Status == PendingSelfEnrollmentStatus.AWAITING_EMAIL_CONFIRMATION ||
                 x.Status == PendingSelfEnrollmentStatus.PENDING) &&
                (x.EmailHash == emailHash || (ibanGiven && x.IBAN_Hash == ibanHash)));
            if (pendingExists)
                return new PendingSelfEnrollmentUpdateResult(PendingSelfEnrollmentActionStatus.DUPLICATE_CONFLICT);
        }

        MemberCategoryEntity? memberCategory = null;
        if (request.MemberCategoryId != null)
        {
            memberCategory = await _db.MemberCategories.FindAsync(request.MemberCategoryId);
            if (memberCategory == null)
                return new PendingSelfEnrollmentUpdateResult(PendingSelfEnrollmentActionStatus.MEMBER_CATEGORY_NOT_FOUND);
        }

        ContributionPlanEntity? contributionPlan = null;
        if (request.ContributionPlanId != null)
        {
            contributionPlan = await _db.ContributionPlans.FindAsync(request.ContributionPlanId);
            if (contributionPlan == null)
                return new PendingSelfEnrollmentUpdateResult(PendingSelfEnrollmentActionStatus.CONTRIBUTION_PLAN_NOT_FOUND);
        }

        PendingSelfEnrollmentEntity snapshot = new()
        {
            ConfirmationTokenHash = pending.ConfirmationTokenHash,
            Gender = pending.Gender,
            FirstName = pending.FirstName,
            MiddleName = pending.MiddleName,
            LastName = pending.LastName,
            BirthdayEncrypted = pending.BirthdayEncrypted,
            StreetEncrypted = pending.StreetEncrypted,
            PostalCode = pending.PostalCode,
            City = pending.City,
            CountryCode = pending.CountryCode,
            EmailEncrypted = pending.EmailEncrypted,
            EmailHash = pending.EmailHash,
            PhoneEncrypted = pending.PhoneEncrypted,
            BulkMail = pending.BulkMail,
            StartOfStudies = pending.StartOfStudies,
            EndOfStudies = pending.EndOfStudies,
            AcademicDegree = pending.AcademicDegree,
            CourseOfStudy = pending.CourseOfStudy,
            MotivationEncrypted = pending.MotivationEncrypted,
            MemberCategoryId = pending.MemberCategoryId,
            MemberCategory = pending.MemberCategory,
            IBAN_Encrypted = pending.IBAN_Encrypted,
            IBAN_Hash = pending.IBAN_Hash,
            Bic_Encrypted = pending.Bic_Encrypted,
            ContributionPlanId = pending.ContributionPlanId,
            ContributionPlan = pending.ContributionPlan
        };

        if (request.Gender != null)
            pending.Gender = (Gender)request.Gender;
        if (request.FirstName != null)
            pending.FirstName = request.FirstName;
        if (request.MiddleName != null)
            pending.MiddleName = request.MiddleName;
        if (request.LastName != null)
            pending.LastName = request.LastName;
        if (request.Birthday != null)
            pending.BirthdayEncrypted = _crypto.Encrypt((DateTimeOffset)request.Birthday);
        if (request.Street != null)
            pending.StreetEncrypted = _crypto.Encrypt(request.Street);
        if (request.PostalCode != null)
            pending.PostalCode = request.PostalCode;
        if (request.City != null)
            pending.City = request.City;
        if (request.CountryCode != null)
            pending.CountryCode = request.CountryCode;
        if (request.Email != null)
        {
            pending.EmailEncrypted = _crypto.Encrypt(request.Email);
            pending.EmailHash = _crypto.Hash(request.Email);
        }

        if (request.Phone != null)
            pending.PhoneEncrypted = _crypto.Encrypt(request.Phone);
        if (request.BulkMail != null)
            pending.BulkMail = (BulkMail)request.BulkMail;
        if (request.StartOfStudies != null)
            pending.StartOfStudies = (DateTimeOffset)request.StartOfStudies;
        if (request.EndOfStudies != null)
            pending.EndOfStudies = request.EndOfStudies;
        if (request.AcademicDegree != null)
            pending.AcademicDegree = request.AcademicDegree;
        if (request.CourseOfStudy != null)
            pending.CourseOfStudy = request.CourseOfStudy;
        if (request.Motivation != null)
            pending.MotivationEncrypted = _crypto.Encrypt(request.Motivation);
        if (request.MemberCategoryId != null)
        {
            pending.MemberCategoryId = (Guid)request.MemberCategoryId;
            pending.MemberCategory = memberCategory;
        }

        if (request.IBAN != null)
        {
            pending.IBAN_Encrypted = _crypto.Encrypt(request.IBAN);
            pending.IBAN_Hash = _crypto.Hash(request.IBAN);
        }

        if (request.Bic != null)
            pending.Bic_Encrypted = _crypto.Encrypt(request.Bic);
        if (request.ContributionPlanId != null)
        {
            pending.ContributionPlanId = request.ContributionPlanId;
            pending.ContributionPlan = contributionPlan;
        }

        _db.Update(pending);
        await _db.SaveChangesAsync();

        List<MemberAuditDeltaEntry> delta = PendingSelfEnrollmentAuditDelta.Compare(snapshot, pending, _crypto);
        if (delta.Count > 0)
            await _auditService.LogAsync(AuditLogActions.UPDATE, nameof(PendingSelfEnrollmentEntity), new
            {
                PendingSelfEnrollmentId = pending.Id,
                Changes = delta
            });

        return new PendingSelfEnrollmentUpdateResult(PendingSelfEnrollmentActionStatus.SUCCESS, pending);
    }

    private async Task SendConfirmationMailAsync(SelfEnrollmentRequest request, string token, string confirmBaseUrl)
    {
        try
        {
            string confirmLink = $"{confirmBaseUrl}?token={Uri.EscapeDataString(token)}";
            string body = $"""
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <meta charset="utf-8" />
                        <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                        <title>Bitte bestätige deine Anmeldung</title>
                    </head>
                    <body style="font-family: Arial, sans-serif; line-height: 1.6; color: #333;">
                        <h2>📩 Bitte bestätige deine Anmeldung / Please confirm your registration</h2>

                        <hr/>
                        <h3>🇩🇪 Deutsch</h3>
                        <p>Hallo {request.FirstName},</p>
                        <p>bitte bestätige deine Selbstregistrierung über den folgenden Link:</p>
                        <p><a href="{confirmLink}">{confirmLink}</a></p>
                        <p>Der Link ist 48 Stunden gültig. Falls du diese Anmeldung nicht ausgelöst hast, kannst du
                        diese E-Mail ignorieren.</p>

                        <hr/>
                        <h3>🇬🇧 English</h3>
                        <p>Hi {request.FirstName},</p>
                        <p>please confirm your self-enrollment using the link below:</p>
                        <p><a href="{confirmLink}">{confirmLink}</a></p>
                        <p>This link is valid for 48 hours. If you did not request this, you can safely ignore this
                        email.</p>
                    </body>
                    </html>
                    """;

            EmailRequest emailRequest = new()
            {
                Subject = "📩 Bitte bestätige deine Anmeldung / Please confirm your registration",
                HtmlBody = body,
                Attachments = []
            };

            await _mailService.SendEmailsAsync(
                [new Recipient { Email = request.Email, FirstName = request.FirstName, LastName = request.LastName }],
                emailRequest, "123456789");
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"PendingSelfEnrollmentService: Error while sending confirmation mail to {request.Email}");
        }
    }

    private async Task SendAcceptanceMailAsync(PendingSelfEnrollmentEntity pending)
    {
        try
        {
            string? email = _crypto.Decrypt(pending.EmailEncrypted);
            if (string.IsNullOrWhiteSpace(email))
            {
                Log.Warning($"PendingSelfEnrollmentService: Pending self-enrollment {pending.Id} has no email, " +
                            "skipping acceptance mail.");
                return;
            }

            string body = $"""
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <meta charset="utf-8" />
                        <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                        <title>Deine Anmeldung wurde angenommen</title>
                    </head>
                    <body style="font-family: Arial, sans-serif; line-height: 1.6; color: #333;">
                        <h2>🎉 Deine Anmeldung wurde angenommen / Your registration was approved</h2>

                        <hr/>
                        <h3>🇩🇪 Deutsch</h3>
                        <p>Hallo {pending.FirstName},</p>
                        <p>deine Selbstregistrierung wurde geprüft und angenommen. Du bist ab sofort Mitglied.</p>

                        <hr/>
                        <h3>🇬🇧 English</h3>
                        <p>Hi {pending.FirstName},</p>
                        <p>your self-enrollment has been reviewed and approved. You are now a member.</p>
                    </body>
                    </html>
                    """;

            EmailRequest emailRequest = new()
            {
                Subject = "🎉 Deine Anmeldung wurde angenommen / Your registration was approved",
                HtmlBody = body,
                Attachments = []
            };

            await _mailService.SendEmailsAsync(
                [new Recipient { Email = email, FirstName = pending.FirstName, LastName = pending.LastName }],
                emailRequest, "123456789");
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"PendingSelfEnrollmentService: Error while sending acceptance mail for pending self-enrollment {pending.Id}");
        }
    }

    private static string GenerateToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    public async Task<int> CleanupExpiredAsync()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        PendingSelfEnrollmentEntity[] expired = await _db.PendingSelfEnrollments
            .Where(x => x.Status == PendingSelfEnrollmentStatus.AWAITING_EMAIL_CONFIRMATION &&
                        x.ConfirmationTokenExpiresAt < now)
            .ToArrayAsync();

        if (expired.Length == 0)
            return 0;

        _db.PendingSelfEnrollments.RemoveRange(expired);
        await _db.ForceSaveChangesAsync();

        return expired.Length;
    }
}
