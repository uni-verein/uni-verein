using System;
using System.Linq;
using System.Threading.Tasks;
using UniVerein.Api.Models.Enums;
using UniVerein.Api.Models.MemberAudit;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using UniVerein.Api.Models;

namespace UniVerein.Api.Services;

public class MemberService
{
    private readonly AppDbContext _db;
    private readonly CryptoService _crypto;
    private readonly AuditService _auditService;

    public MemberService(AppDbContext db, CryptoService crypto, AuditService auditService)
    {
        _db = db;
        _crypto = crypto;
        _auditService = auditService;
    }

    public async Task<MemberCreationResult> CreateMemberAsync(MemberCreationInput input)
    {
        string iban = _crypto.Hash(input.IBAN);
        string email = _crypto.Hash(input.Email);
        bool ibanGiven = !string.IsNullOrWhiteSpace(input.IBAN);

        bool memberExists =
            await _db.Members.AnyAsync(x => (x.EmailHash == email) || (ibanGiven && x.IBAN_Hash == iban));
        if (memberExists)
            return MemberCreationResult.Failure(MemberCreationStatus.DUPLICATE_CONFLICT);

        ContributionPlanEntity? contributionPlan = null;
        if (input.ContributionPlanId != null)
        {
            contributionPlan = await _db.ContributionPlans.FindAsync(input.ContributionPlanId);
            if (contributionPlan == null)
                return MemberCreationResult.Failure(MemberCreationStatus.CONTRIBUTION_PLAN_NOT_FOUND);
        }

        MemberCategoryEntity? memberCategory = await _db.MemberCategories.FindAsync(input.MemberCategoryId);
        if (memberCategory == null)
            return MemberCreationResult.Failure(MemberCreationStatus.MEMBER_CATEGORY_NOT_FOUND);

        int maxMemberNumber = await _db.Members.Select(m => (int?)m.MemberNumber).MaxAsync() ?? 0;
        int newMemberNumber = maxMemberNumber + 1;

        MemberEntity member = new()
        {
            MandateId = $"{DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss")}_{newMemberNumber}",
            MemberNumber = newMemberNumber,
            Gender = input.Gender,
            FirstName = input.FirstName,
            MiddleName = input.MiddleName,
            LastName = input.LastName,
            BirthdayEncrypted = _crypto.Encrypt(input.Birthday),
            StreetEncrypted = _crypto.Encrypt(input.Street),
            PostalCode = input.PostalCode,
            City = input.City,
            CountryCode = input.CountryCode,
            EmailEncrypted = _crypto.Encrypt(input.Email),
            EmailHash = _crypto.Hash(input.Email),
            PhoneEncrypted = _crypto.Encrypt(input.Phone),
            BulkMail = input.BulkMail,
            StartOfStudies = input.StartOfStudies,
            EndOfStudies = input.EndOfStudies,
            AcademicDegree = input.AcademicDegree,
            CourseOfStudy = input.CourseOfStudy,
            TaskWithinTheClub = input.TaskWithinTheClub,
            MemberCategoryId = input.MemberCategoryId,
            MemberCategory = memberCategory,
            IBAN_Encrypted = _crypto.Encrypt(input.IBAN),
            IBAN_Hash = _crypto.Hash(input.IBAN),
            Bic_Encrypted = _crypto.Encrypt(input.Bic),
            SepaConsent = input.SepaConsent,
            EntryDate = input.EntryDate,
            ExitDate = input.ExitDate,
            ContributionPlanId = input.ContributionPlanId,
            ContributionPlan = contributionPlan
        };

        await _db.Members.AddAsync(member);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(AuditLogActions.CREATE, nameof(MemberEntity), new MemberAudit
        {
            MemberId = member.Id,
            MemberNumber = member.MemberNumber,
            MandateId = member.MandateId,
            Gender = member.Gender,
            MemberCategory = member.MemberCategory.Name,
            TaskWithinTheClub = member.TaskWithinTheClub,
            AcademicDegree = member.AcademicDegree,
            CourseOfStudy = member.CourseOfStudy,
            StartOfStudies = member.StartOfStudies,
            EndOfStudies = member.EndOfStudies,
            EntryDate = member.EntryDate,
            ExitDate = member.ExitDate,
            BulkMail = member.BulkMail,
            ContributionPlanId = member.ContributionPlanId,
            HasIban = !string.IsNullOrWhiteSpace(input.IBAN),
            HasBic = !string.IsNullOrWhiteSpace(input.Bic),
            HasSepaConsent = member.SepaConsent.HasValue
        });

        return MemberCreationResult.Success(member);
    }
}
