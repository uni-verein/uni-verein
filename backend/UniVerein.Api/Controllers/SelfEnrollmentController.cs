using System.Threading.Tasks;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Services;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using UniVerein.Api.Models;
using UniVerein.Api.Models.Enums;

namespace UniVerein.Api.Controllers;

[ApiController]
[Route("self-enrollment")]
[EnableCors("AllowFrontend")]
public class SelfEnrollmentController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly MemberService _memberService;
    private readonly ReferenceDataService _referenceDataService;

    public SelfEnrollmentController(AppDbContext db, MemberService memberService, ReferenceDataService referenceDataService)
    {
        _db = db;
        _memberService = memberService;
        _referenceDataService = referenceDataService;
    }

    private async Task<bool> IsEnabledAsync()
    {
        WebPageConfigEntity? config = await _db.WebPageConfigs.FirstOrDefaultAsync(x => x.DeletedAt == null);

        return config?.SelfEnrollmentEnabled ?? false;
    }

    [HttpGet("form-data")]
    public async Task<ActionResult<SelfEnrollmentFormDataResult>> GetFormDataAsync()
    {
        SelfEnrollmentFormDataResult result = new();
        if (!await IsEnabledAsync())
            return Ok(result);

        result.MemberCategories = await _referenceDataService.GetMemberCategoriesAsync();
        result.ContributionPlans = await _referenceDataService.GetContributionPlansAsync();

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<MemberResult>> CreateAsync([FromBody] SelfEnrollmentRequest request)
    {
        Log.Information(
            $"SelfEnrollmentController: CreateAsync -> Try to self-enroll member: {request.FirstName} {request.LastName}");

        if (!await IsEnabledAsync())
        {
            Log.Warning("SelfEnrollmentController: CreateAsync -> Self-enrollment is disabled");
            return StatusCode(StatusCodes.Status403Forbidden, new ApiResults.ErrorResults.ForbiddenRequestResult(
                errorMessage: "Self-enrollment is disabled.",
                moreInfo: "Self-enrollment is currently not enabled for this club."));
        }

        MemberCreationResult result = await _memberService.CreateMemberAsync(new MemberCreationInput
        {
            Gender = request.Gender,
            FirstName = request.FirstName,
            MiddleName = request.MiddleName,
            LastName = request.LastName,
            Birthday = request.Birthday,
            Street = request.Street,
            PostalCode = request.PostalCode,
            City = request.City,
            CountryCode = request.CountryCode,
            Email = request.Email,
            Phone = request.Phone,
            BulkMail = request.BulkMail,
            StartOfStudies = request.StartOfStudies,
            EndOfStudies = request.EndOfStudies,
            AcademicDegree = request.AcademicDegree,
            CourseOfStudy = request.CourseOfStudy,
            TaskWithinTheClub = TaskWithinTheClub.MEMBER,
            MemberCategoryId = request.MemberCategoryId,
            IBAN = request.IBAN,
            Bic = request.Bic,
            SepaConsent = request.SepaConsent,
            EntryDate = request.EntryDate,
            ExitDate = null,
            ContributionPlanId = request.ContributionPlanId
        });

        switch (result.Status)
        {
            case MemberCreationStatus.DUPLICATE_CONFLICT:
                Log.Warning("SelfEnrollmentController: CreateAsync -> Member already exists");
                return Conflict(new ApiResults.ErrorResults.ConflictResult(errorCode: ApiErrorCodes.CONFLICT_RESOURCE_ALREADY_EXISTS,
                    errorMessage: "Member already exists.",
                    moreInfo: "A member with the same email address or IBAN already exists."));
            case MemberCreationStatus.CONTRIBUTION_PLAN_NOT_FOUND:
                Log.Warning(
                    $"SelfEnrollmentController: CreateAsync -> ContributionPlan with ID: {request.ContributionPlanId} not found");
                return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                    errorMessage: "Contribution plan not found.",
                    moreInfo: $"No contribution plan found with ID {request.ContributionPlanId}."));
            case MemberCreationStatus.MEMBER_CATEGORY_NOT_FOUND:
                Log.Warning(
                    $"SelfEnrollmentController: CreateAsync -> Member category with ID: {request.MemberCategoryId} not found");
                return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                    errorMessage: "Member category not found.",
                    moreInfo: $"No member category found with ID {request.MemberCategoryId}."));
        }

        MemberEntity member = result.Member!;

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
            CountryCode = member.CountryCode ?? string.Empty,
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
            $"SelfEnrollmentController: CreateAsync -> Member: {request.FirstName} {request.LastName} successfully self-enrolled.");
        return Created(string.Empty, memberResult);
    }
}
