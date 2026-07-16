using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Models.Enums;
using UniVerein.Api.Models.MemberAudit;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Query;
using Microsoft.AspNetCore.Mvc;
using UniVerein.Api.Services;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using F23.StringSimilarity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace UniVerein.Api.Controllers;

[Authorize]
[ApiController]
[Route("members")]
[EnableCors("AllowFrontend")]
public class MembersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly CryptoService _crypto;
    private readonly AuditService _auditService;
    private readonly IHttpContextAccessor _http;

    public MembersController(AppDbContext db, CryptoService crypto, AuditService auditService,
        IHttpContextAccessor http)
    {
        _db = db;
        _crypto = crypto;
        _auditService = auditService;
        _http = http;
    }

    [HttpGet]
    public async Task<ActionResult<AllMemberResults>> GetAllAsync([FromQuery] MemberQuery memberQuery)
    {
        if (memberQuery.Offset < 0 || memberQuery.Limit < 1)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Offset and/or Limit must be greater than or equal to 1."));

        bool isAdmin = _http.HttpContext?.User.FindFirst(ClaimTypes.Role)?.Value == nameof(UserRole.ADMIN);
        IQueryable<MemberEntity> query = _db.Members.Include(x => x.MemberCategory).AsQueryable();
        if (!isAdmin)
            query = query.Where(m => m.DeletedAt == null);

        if (isAdmin && memberQuery.Deleted == true)
            query = query.Where(m => m.DeletedAt != null);

        if (memberQuery.MemberCategoryId != null)
            query = query.Where(m => m.MemberCategoryId == memberQuery.MemberCategoryId);

        if (memberQuery.TaskWithinTheClub != null)
            query = query.Where(m => m.TaskWithinTheClub == memberQuery.TaskWithinTheClub);

        IQueryable<MemberResult> memberResultQuery = query.Select(m => new MemberResult()
        {
            Id = m.Id,
            MemberNumber = m.MemberNumber,
            Gender = m.Gender,
            FirstName = m.FirstName,
            MiddleName = m.MiddleName,
            LastName = m.LastName,
            Birthday = _crypto.DecryptDate(m.BirthdayEncrypted) ?? DateTimeOffset.MinValue,
            Street = _crypto.Decrypt(m.StreetEncrypted) ?? string.Empty,
            PostalCode = m.PostalCode,
            City = m.City,
            CountryCode = m.CountryCode ?? string.Empty,
            Email = _crypto.Decrypt(m.EmailEncrypted) ?? string.Empty,
            Phone = _crypto.Decrypt(m.PhoneEncrypted) ?? string.Empty,
            BulkMail = m.BulkMail,
            StartOfStudies = m.StartOfStudies,
            EndOfStudies = m.EndOfStudies,
            AcademicDegree = m.AcademicDegree,
            CourseOfStudy = m.CourseOfStudy,
            TaskWithinTheClub = m.TaskWithinTheClub,
            MemberCategoryId = m.MemberCategoryId,
            IBAN = _crypto.Decrypt(m.IBAN_Encrypted) ?? string.Empty,
            Bic = _crypto.Decrypt(m.Bic_Encrypted) ?? string.Empty,
            SepaConsent = m.SepaConsent,
            EntryDate = m.EntryDate,
            ExitDate = m.ExitDate,
            ContributionPlanId = m.ContributionPlanId,
            DeletedAt = m.DeletedAt
        });

        List<MemberResult> memberResults;
        int total = 0;
        if (!string.IsNullOrEmpty(memberQuery.Name))
        {
            memberResults = await memberResultQuery.ToListAsync();
            JaroWinkler jaroWinkler = new();
            memberResults = memberResults.Select(x => new
                {
                    member = x,
                    similarity = jaroWinkler.Similarity($"{x.FirstName} {x.MiddleName} {x.LastName}".ToLower(),
                        memberQuery.Name.ToLower())
                })
                .OrderBy(x => x.similarity)
                .Where(x => x.similarity > 0.75)
                .Select(x => x.member)
                .ToList();
            total = memberResults.Count;
        }
        else
        {
            memberResultQuery = memberResultQuery.OrderBy(x => x.MemberNumber);
            total = await memberResultQuery.CountAsync();
            memberResults = await memberResultQuery.ToListAsync();
        }

        AllMemberResults result = new()
        {
            Items = memberResults.Skip(memberQuery.Offset).Take(memberQuery.Limit).ToList(),
            Total = total
        };

        return Ok(result);
    }

    [HttpGet("count")]
    public async Task<ActionResult<MemberCountResult>> GetMemberCountAsync([FromQuery] Guid? memberCategoryId)
    {
        IQueryable<MemberEntity> query = _db.Members.AsQueryable().Where(m => m.DeletedAt == null);

        if (memberCategoryId != null)
            query = query.Where(m => m.MemberCategoryId == memberCategoryId);

        MemberCountResult result = new()
        {
            Count = await query.CountAsync()
        };

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<MemberResult>> CreateAsync([FromBody] MemberRequest request)
    {
        Log.Information(
            $"MembersController: CreateAsync -> Try to create member: {request.FirstName} {request.LastName}");

        string iban = _crypto.Hash(request.IBAN);
        string email = _crypto.Hash(request.Email);
        bool ibanGiven = !string.IsNullOrWhiteSpace(request.IBAN);

        bool memberExists =
            await _db.Members.AnyAsync(x => (x.EmailHash == email) || (ibanGiven && x.IBAN_Hash == iban));
        if (memberExists)
        {
            Log.Warning($"MembersController: CreateAsync -> Member already exists");
            return Conflict(new ApiResults.ErrorResults.ConflictResult(errorCode: ApiErrorCodes.CONFLICT_RESOURCE_ALREADY_EXISTS,
                errorMessage: "Member already exists.",
                moreInfo: "A member with the same email address or IBAN already exists."));
        }

        ContributionPlanEntity? contributionPlan = null;
        if (request.ContributionPlanId != null)
        {
            contributionPlan = await _db.ContributionPlans.FindAsync(request.ContributionPlanId);
            if (contributionPlan == null)
            {
                Log.Warning(
                    $"MembersController: CreateAsync -> ContributionPlan with ID: {request.ContributionPlanId} not found");
                return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                    errorMessage: "Contribution plan not found.",
                    moreInfo: $"No contribution plan found with ID {request.ContributionPlanId}."));
            }
        }

        MemberCategoryEntity? memberCategory = await _db.MemberCategories.FindAsync(request.MemberCategoryId);
        if (memberCategory == null)
        {
            Log.Warning(
                $"MembersController: CreateAsync -> ContributionPlan with ID: {request.ContributionPlanId} not found");
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Member category not found.",
                moreInfo: $"No member category found with ID {request.MemberCategoryId}."));
        }


        int maxMemberNumber = await _db.Members.Select(m => (int?)m.MemberNumber).MaxAsync() ?? 0;
        int newMemberNumber = maxMemberNumber + 1;

        MemberEntity member = new()
        {
            MandateId = $"{DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss")}_{newMemberNumber}",
            MemberNumber = newMemberNumber,
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
            EmailHash = _crypto.Hash(request.Email),
            PhoneEncrypted = _crypto.Encrypt(request.Phone),
            BulkMail = request.BulkMail,
            StartOfStudies = request.StartOfStudies,
            EndOfStudies = request.EndOfStudies,
            AcademicDegree = request.AcademicDegree,
            CourseOfStudy = request.CourseOfStudy,
            TaskWithinTheClub = request.TaskWithinTheClub,
            MemberCategoryId = request.MemberCategoryId,
            MemberCategory = memberCategory,
            IBAN_Encrypted = _crypto.Encrypt(request.IBAN),
            IBAN_Hash = _crypto.Hash(request.IBAN),
            Bic_Encrypted = _crypto.Encrypt(request.Bic),
            SepaConsent = request.SepaConsent,
            EntryDate = request.EntryDate,
            ExitDate = request.ExitDate,
            ContributionPlanId = request.ContributionPlanId,
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
            HasIban = !string.IsNullOrWhiteSpace(request.IBAN),
            HasBic = !string.IsNullOrWhiteSpace(request.Bic),
            HasSepaConsent = member.SepaConsent.HasValue
        });

        MemberResult memberResult = new()
        {
            Id = member.Id,
            MemberNumber = member.MemberNumber,
            Gender = member.Gender,
            FirstName = member.FirstName,
            MiddleName = member.MiddleName,
            LastName = member.LastName,
            Birthday = request.Birthday,
            Street = request.Street,
            PostalCode = member.PostalCode,
            City = member.City,
            CountryCode = member.CountryCode,
            Email = request.Email,
            Phone = request.Phone,
            BulkMail = member.BulkMail,
            StartOfStudies = member.StartOfStudies,
            EndOfStudies = member.EndOfStudies,
            AcademicDegree = member.AcademicDegree,
            CourseOfStudy = member.CourseOfStudy,
            TaskWithinTheClub = member.TaskWithinTheClub,
            MemberCategoryId = member.MemberCategoryId,
            IBAN = request.IBAN ?? string.Empty,
            Bic = request.Bic,
            SepaConsent = member.SepaConsent,
            EntryDate = member.EntryDate,
            ExitDate = member.ExitDate,
            ContributionPlanId = member.ContributionPlanId
        };

        Log.Information(
            $"MembersController: CreateAsync -> Member: {request.FirstName} {request.LastName} successfully created.");
        return Created(string.Empty, memberResult);
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult<MemberResult>> UpdateAsync([FromRoute] Guid id,
        [FromBody] MemberUpdateRequest request)
    {
        Log.Information($"MembersController: UpdateAsync -> Try to update member with ID: {id}");

        MemberEntity? member = await _db.Members.FindAsync(id);
        if (member == null)
        {
            Log.Warning($"MembersController: UpdateAsync -> Member with ID: {id} not found");
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Member with ID not found.",
                moreInfo: $"No member with the ID {id} could be found."));
        }

        string iban = _crypto.Hash(request.IBAN ?? "");
        string email = _crypto.Hash(request.Email ?? "");
        bool ibanGiven = !string.IsNullOrWhiteSpace(request.IBAN);
        if (await _db.Members.AnyAsync(x =>
                x.Id != id && ((x.EmailHash == email) || (ibanGiven && x.IBAN_Hash == iban))))
        {
            Log.Warning(
                $"MembersController: UpdateAsync -> Member with ID: {id} already exists (email or IBAN are equal with a other member)");
            return Conflict(new ApiResults.ErrorResults.ConflictResult(errorCode: ApiErrorCodes.CONFLICT_RESOURCE_ALREADY_EXISTS,
                errorMessage: "Member already exists.",
                moreInfo: "A member with the same email address or IBAN already exists."));
        }

        ContributionPlanEntity? contributionPlan = null;
        if (request.ContributionPlanId != null)
        {
            contributionPlan = await _db.ContributionPlans.FindAsync(request.ContributionPlanId);
            if (contributionPlan == null)
            {
                Log.Warning(
                    $"MembersController: UpdateAsync -> ContributionPlan with ID: {request.ContributionPlanId} not found");
                return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                    errorMessage: "Contribution plan not found.",
                    moreInfo: $"No contribution plan found with ID {request.ContributionPlanId}."));
            }
        }

        MemberCategoryEntity? memberCategory = null;
        if (request.MemberCategoryId != null)
        {
            memberCategory = await _db.MemberCategories.FindAsync(request.MemberCategoryId);
            if (memberCategory == null)
            {
                Log.Warning(
                    $"MembersController: UpdateAsync -> Member category with ID: {request.MemberCategoryId} not found");
                return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                    errorMessage: "Member category not found.",
                    moreInfo: $"No member category found with ID {request.MemberCategoryId}."));
            }
        }

        MemberEntity snapshot = new()
        {
            MandateId = member.MandateId,
            Gender = member.Gender,
            FirstName = member.FirstName,
            MiddleName = member.MiddleName,
            LastName = member.LastName,
            BirthdayEncrypted = member.BirthdayEncrypted,
            StreetEncrypted = member.StreetEncrypted,
            PostalCode = member.PostalCode,
            City = member.City,
            CountryCode =  member.CountryCode,
            EmailEncrypted = member.EmailEncrypted,
            EmailHash = member.EmailHash,
            BulkMail = member.BulkMail,
            PhoneEncrypted = member.PhoneEncrypted,
            StartOfStudies = member.StartOfStudies,
            EndOfStudies = member.EndOfStudies,
            AcademicDegree = member.AcademicDegree,
            CourseOfStudy = member.CourseOfStudy,
            TaskWithinTheClub = member.TaskWithinTheClub,
            MemberCategoryId = member.MemberCategoryId,
            IBAN_Encrypted = member.IBAN_Encrypted,
            Bic_Encrypted = member.Bic_Encrypted,
            SepaConsent = member.SepaConsent,
            EntryDate = member.EntryDate,
            ExitDate = member.ExitDate,
            ContributionPlanId = member.ContributionPlanId
        };
        
        if (snapshot.MemberCategoryId != null)
            snapshot.MemberCategory = await _db.MemberCategories.FindAsync(snapshot.MemberCategoryId);
        if (snapshot.ContributionPlanId != null)
            snapshot.ContributionPlan = await _db.ContributionPlans.FindAsync(snapshot.ContributionPlanId);
        
        if (request.Gender != null)
            member.Gender = (Gender)request.Gender;
        if (request.FirstName != null)
            member.FirstName = request.FirstName;
        if (request.MiddleName != null)
            member.MiddleName = request.MiddleName;
        if (request.LastName != null)
            member.LastName = request.LastName;
        if (request.Birthday != null)
            member.BirthdayEncrypted = _crypto.Encrypt((DateTimeOffset)request.Birthday);
        if (request.Street != null)
            member.StreetEncrypted = _crypto.Encrypt(request.Street);
        if (request.PostalCode != null)
            member.PostalCode = request.PostalCode;
        if (request.City != null)
            member.City = request.City;
        if (request.CountryCode != null)
            member.CountryCode = request.CountryCode;
        if (request.Email != null)
        {
            member.EmailEncrypted = _crypto.Encrypt(request.Email);
            member.EmailHash = _crypto.Hash(request.Email);
        }

        if (request.Phone != null)
            member.PhoneEncrypted = _crypto.Encrypt(request.Phone);
        if (request.BulkMail != null)
            member.BulkMail = (BulkMail)request.BulkMail;
        if (request.StartOfStudies != null)
            member.StartOfStudies = (DateTimeOffset)request.StartOfStudies;
        if (request.EndOfStudies != null)
            member.EndOfStudies = request.EndOfStudies;
        if (request.AcademicDegree != null)
            member.AcademicDegree = request.AcademicDegree;
        if (request.CourseOfStudy != null)
            member.CourseOfStudy = request.CourseOfStudy;
        if (request.TaskWithinTheClub != null)
            member.TaskWithinTheClub = (TaskWithinTheClub)request.TaskWithinTheClub;
        if (request.MemberCategoryId != null)
        {
            member.MemberCategoryId = request.MemberCategoryId;
            member.MemberCategory = memberCategory;
        }

        if (request.IBAN != null)
        {
            member.IBAN_Encrypted = _crypto.Encrypt(request.IBAN);
            member.IBAN_Hash = _crypto.Hash(request.IBAN);
        }

        if (request.Bic != null)
            member.Bic_Encrypted = _crypto.Encrypt(request.Bic);
        if (request.SepaConsent != null)
            member.SepaConsent = request.SepaConsent;
        if (request.EntryDate != null)
            member.EntryDate = (DateTimeOffset)request.EntryDate;
        if (request.ExitDate != null)
            member.ExitDate = request.ExitDate;
        if (request.ContributionPlanId != null)
        {
            member.ContributionPlanId = request.ContributionPlanId;
            member.ContributionPlan = contributionPlan;
        }

        _db.Update(member);
        await _db.SaveChangesAsync();

        var delta = MemberAuditDelta.Compare(snapshot, member, _crypto);
        if (delta.Count > 0)
            await _auditService.LogAsync(AuditLogActions.UPDATE, nameof(MemberEntity), new
            {
                MemberId = id,
                Changes = delta
            });

        MemberResult memberResult = new()
        {
            Id = member.Id,
            MemberNumber = member.MemberNumber,
            Gender = member.Gender,
            FirstName = member.FirstName,
            MiddleName = member.MiddleName,
            LastName = member.LastName,
            Birthday = request.Birthday ?? _crypto.DecryptDate(member.BirthdayEncrypted) ?? DateTimeOffset.MinValue,
            Street = request.Street ?? _crypto.Decrypt(member.StreetEncrypted) ?? string.Empty,
            PostalCode = member.PostalCode,
            City = member.City,
            CountryCode = member.CountryCode ?? string.Empty,
            Email = request.Email ?? _crypto.Decrypt(member.EmailEncrypted) ?? string.Empty,
            Phone = request.Phone ?? _crypto.Decrypt(member.PhoneEncrypted) ?? string.Empty,
            BulkMail = member.BulkMail,
            StartOfStudies = member.StartOfStudies,
            EndOfStudies = member.EndOfStudies,
            AcademicDegree = member.AcademicDegree,
            CourseOfStudy = member.CourseOfStudy,
            TaskWithinTheClub = member.TaskWithinTheClub,
            MemberCategoryId = member.MemberCategoryId,
            IBAN = request.IBAN ?? _crypto.Decrypt(member.IBAN_Encrypted) ?? string.Empty,
            Bic = request.Bic ?? _crypto.Decrypt(member.Bic_Encrypted) ?? string.Empty,
            SepaConsent = member.SepaConsent,
            EntryDate = member.EntryDate,
            ExitDate = member.ExitDate,
            ContributionPlanId = member.ContributionPlanId
        };

        Log.Information($"MembersController: UpdateAsync -> Member with ID: {member.Id} successfully updated.");
        return Ok(memberResult);
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpPost("{id}")]
    public async Task<IActionResult> RestoreAsync([FromRoute] Guid id)
    {
        Log.Information($"MembersController: RestoreAsync -> Try to restore member with ID: {id}");

        MemberEntity? member = await _db.Members.FindAsync(id);
        if (member == null)
        {
            Log.Warning($"MembersController: RestoreAsync -> Member with ID: {id} not found.");
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Member with ID not found.",
                moreInfo: $"No member with the ID {id} could be found."));
        }

        member.DeletedAt = null;
        _db.Update(member);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(AuditLogActions.RESTORE, nameof(MemberEntity),
            new { MemberId = id, member.MemberNumber });

        Log.Information($"MembersController: RestoreAsync -> Member with ID: {id} successfully restored.");
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] Guid id)
    {
        MemberEntity? member = await _db.Members.FindAsync(id);
        if (member == null)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Member with ID not found.",
                moreInfo: $"No member with the ID {id} could be found."));

        _db.Remove(member);
        bool isAdmin = _http.HttpContext?.User.FindFirst(ClaimTypes.Role)?.Value == nameof(UserRole.ADMIN);
        if (isAdmin)
        {
            await _db.ForceSaveChangesAsync();
            await _auditService.LogAsync(AuditLogActions.DELETE, nameof(MemberEntity),
                new { MemberId = id, member.MemberNumber });
        }
        else
        {
            await _db.SaveChangesAsync();
            await _auditService.LogAsync(AuditLogActions.SOFT_DELETE, nameof(MemberEntity),
                new { MemberId = id, member.MemberNumber });
        }

        return Ok();
    }
}