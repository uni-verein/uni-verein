using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Models;
using UniVerein.Api.Models.Enums;
using UniVerein.Api.Query;
using UniVerein.Api.Services;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace UniVerein.Api.Controllers;

[Authorize]
[ApiController]
[Route("pending-self-enrollments")]
[EnableCors("AllowFrontend")]
public class PendingSelfEnrollmentController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly CryptoService _crypto;
    private readonly PendingSelfEnrollmentService _pendingSelfEnrollmentService;

    public PendingSelfEnrollmentController(AppDbContext db, CryptoService crypto,
        PendingSelfEnrollmentService pendingSelfEnrollmentService)
    {
        _db = db;
        _crypto = crypto;
        _pendingSelfEnrollmentService = pendingSelfEnrollmentService;
    }

    [HttpGet]
    public async Task<ActionResult<PendingSelfEnrollmentResults>> GetAllAsync([FromQuery] QueryBase query)
    {
        if (query.Offset < 0 || query.Limit < 1)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: "Offset and/or Limit must be greater than or equal to 1."));

        IQueryable<PendingSelfEnrollmentEntity> pendingQuery = _db.PendingSelfEnrollments
            .Include(x => x.MemberCategory)
            .Where(x => x.Status == PendingSelfEnrollmentStatus.PENDING)
            .OrderBy(x => x.CreatedAt);

        int total = await pendingQuery.CountAsync();
        List<PendingSelfEnrollmentEntity> pending = await pendingQuery
            .Skip(query.Offset)
            .Take(query.Limit)
            .ToListAsync();

        List<PendingSelfEnrollmentResult> items = pending.Select(x => new PendingSelfEnrollmentResult
        {
            Id = x.Id,
            Gender = x.Gender,
            FirstName = x.FirstName,
            MiddleName = x.MiddleName,
            LastName = x.LastName,
            Email = _crypto.Decrypt(x.EmailEncrypted) ?? string.Empty,
            MemberCategoryId = x.MemberCategoryId,
            MemberCategoryName = x.MemberCategory?.Name,
            ContributionPlanId = x.ContributionPlanId,
            SubmittedAt = x.CreatedAt,
            SubmittedIp = x.SubmittedIp,
            ConfirmedAt = x.ConfirmedAt
        }).ToList();

        return Ok(new PendingSelfEnrollmentResults { Items = items, Total = total });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PendingSelfEnrollmentDetailResult>> GetByIdAsync([FromRoute] Guid id)
    {
        PendingSelfEnrollmentEntity? pending = await _pendingSelfEnrollmentService.GetByIdAsync(id);
        if (pending == null)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Pending self-enrollment not found.",
                moreInfo: $"No pending self-enrollment with the ID {id} could be found."));

        return Ok(GetDetailResult(pending));
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult<PendingSelfEnrollmentDetailResult>> UpdateAsync([FromRoute] Guid id,
        [FromBody] PendingSelfEnrollmentUpdateRequest request)
    {
        Log.Information($"PendingSelfEnrollmentController: UpdateAsync -> Try to update pending self-enrollment {id}");

        PendingSelfEnrollmentUpdateResult result = await _pendingSelfEnrollmentService.UpdateAsync(id, request);

        switch (result.Status)
        {
            case PendingSelfEnrollmentActionStatus.NOT_FOUND:
                return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                    errorMessage: "Pending self-enrollment not found.",
                    moreInfo: $"No pending self-enrollment with the ID {id} could be found."));
            case PendingSelfEnrollmentActionStatus.DUPLICATE_CONFLICT:
                return Conflict(new ApiResults.ErrorResults.ConflictResult(errorCode: ApiErrorCodes.CONFLICT_RESOURCE_ALREADY_EXISTS,
                    errorMessage: "Member already exists.",
                    moreInfo: "A member with the same email address or IBAN already exists."));
            case PendingSelfEnrollmentActionStatus.CONTRIBUTION_PLAN_NOT_FOUND:
                return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                    errorMessage: "Contribution plan not found.",
                    moreInfo: $"No contribution plan found with ID {request.ContributionPlanId}."));
            case PendingSelfEnrollmentActionStatus.MEMBER_CATEGORY_NOT_FOUND:
                return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                    errorMessage: "Member category not found.",
                    moreInfo: $"No member category found with ID {request.MemberCategoryId}."));
        }

        Log.Information($"PendingSelfEnrollmentController: UpdateAsync -> Pending self-enrollment {id} updated");
        return Ok(GetDetailResult(result.Pending!));
    }

    private PendingSelfEnrollmentDetailResult GetDetailResult(PendingSelfEnrollmentEntity pending)
    {
        return new PendingSelfEnrollmentDetailResult
        {
            Id = pending.Id,
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
            Motivation = _crypto.Decrypt(pending.MotivationEncrypted) ?? string.Empty,
            MemberCategoryId = pending.MemberCategoryId,
            MemberCategoryName = pending.MemberCategory?.Name,
            IBAN = _crypto.Decrypt(pending.IBAN_Encrypted) ?? string.Empty,
            Bic = _crypto.Decrypt(pending.Bic_Encrypted) ?? string.Empty,
            ContributionPlanId = pending.ContributionPlanId,
            SubmittedAt = pending.CreatedAt,
            SubmittedIp = pending.SubmittedIp,
            ConfirmedAt = pending.ConfirmedAt
        };
    }

    [HttpPost("{id}/approve")]
    public async Task<ActionResult<MemberResult>> ApproveAsync([FromRoute] Guid id)
    {
        Log.Information($"PendingSelfEnrollmentController: ApproveAsync -> Try to approve pending self-enrollment {id}");

        PendingSelfEnrollmentApprovalResult result = await _pendingSelfEnrollmentService.ApproveAsync(id);

        switch (result.Status)
        {
            case PendingSelfEnrollmentActionStatus.NOT_FOUND:
                return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                    errorMessage: "Pending self-enrollment not found.",
                    moreInfo: $"No pending self-enrollment with the ID {id} could be found."));
            case PendingSelfEnrollmentActionStatus.DUPLICATE_CONFLICT:
                return Conflict(new ApiResults.ErrorResults.ConflictResult(errorCode: ApiErrorCodes.CONFLICT_RESOURCE_ALREADY_EXISTS,
                    errorMessage: "Member already exists.",
                    moreInfo: "A member with the same email address or IBAN already exists."));
            case PendingSelfEnrollmentActionStatus.CONTRIBUTION_PLAN_NOT_FOUND:
                return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                    errorMessage: "Contribution plan not found.",
                    moreInfo: "The contribution plan referenced by this submission no longer exists."));
            case PendingSelfEnrollmentActionStatus.MEMBER_CATEGORY_NOT_FOUND:
                return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                    errorMessage: "Member category not found.",
                    moreInfo: "The member category referenced by this submission no longer exists."));
            case PendingSelfEnrollmentActionStatus.MEMBER_CATEGORY_NOT_SET:
                return UnprocessableEntity(new ApiResults.ErrorResults.UnprocessableEntityResult(
                    errorCode: ApiErrorCodes.UNPROCESSABLE_ENTITY,
                    errorMessage: "Member category not set.",
                    moreInfo: "This submission has no assigned member category yet. Assign one before approving."));
        }

        MemberEntity member = result.Member!;
        Log.Information($"PendingSelfEnrollmentController: ApproveAsync -> Pending self-enrollment {id} approved as member {member.Id}");

        return Created(string.Empty, new MemberResult
        {
            Id = member.Id,
            MemberNumber = member.MemberNumber,
            Gender = member.Gender,
            FirstName = member.FirstName,
            MiddleName = member.MiddleName,
            LastName = member.LastName,
            Birthday = _crypto.DecryptDate(member.BirthdayEncrypted) ?? DateTimeOffset.MinValue,
            Street = _crypto.Decrypt(member.StreetEncrypted) ?? string.Empty,
            PostalCode = member.PostalCode,
            City = member.City,
            CountryCode = member.CountryCode ?? string.Empty,
            Email = _crypto.Decrypt(member.EmailEncrypted) ?? string.Empty,
            Phone = _crypto.Decrypt(member.PhoneEncrypted) ?? string.Empty,
            BulkMail = member.BulkMail,
            StartOfStudies = member.StartOfStudies,
            EndOfStudies = member.EndOfStudies,
            AcademicDegree = member.AcademicDegree,
            CourseOfStudy = member.CourseOfStudy,
            TaskWithinTheClub = member.TaskWithinTheClub,
            MemberCategoryId = member.MemberCategoryId,
            IBAN = _crypto.Decrypt(member.IBAN_Encrypted) ?? string.Empty,
            Bic = _crypto.Decrypt(member.Bic_Encrypted) ?? string.Empty,
            SepaConsent = member.SepaConsent,
            EntryDate = member.EntryDate,
            ExitDate = member.ExitDate,
            ContributionPlanId = member.ContributionPlanId
        });
    }

    [HttpPost("{id}/reject")]
    public async Task<IActionResult> RejectAsync([FromRoute] Guid id, [FromBody] RejectPendingSelfEnrollmentRequest request)
    {
        Log.Information($"PendingSelfEnrollmentController: RejectAsync -> Try to reject pending self-enrollment {id}");

        PendingSelfEnrollmentActionStatus status = await _pendingSelfEnrollmentService.RejectAsync(id, request.Reason);
        if (status == PendingSelfEnrollmentActionStatus.NOT_FOUND)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Pending self-enrollment not found.",
                moreInfo: $"No pending self-enrollment with the ID {id} could be found."));

        Log.Information($"PendingSelfEnrollmentController: RejectAsync -> Pending self-enrollment {id} rejected");
        return Ok();
    }
}
