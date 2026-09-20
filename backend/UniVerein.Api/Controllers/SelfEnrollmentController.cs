using System.Threading.Tasks;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Extensions;
using UniVerein.Api.Models;
using UniVerein.Api.Models.Enums;
using UniVerein.Api.Services;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace UniVerein.Api.Controllers;

[ApiController]
[Route("self-enrollment")]
[EnableCors("AllowFrontend")]
public class SelfEnrollmentController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PendingSelfEnrollmentService _pendingSelfEnrollmentService;
    private readonly ReferenceDataService _referenceDataService;

    public SelfEnrollmentController(AppDbContext db, PendingSelfEnrollmentService pendingSelfEnrollmentService,
        ReferenceDataService referenceDataService)
    {
        _db = db;
        _pendingSelfEnrollmentService = pendingSelfEnrollmentService;
        _referenceDataService = referenceDataService;
    }

    private async Task<bool> IsEnabledAsync()
    {
        WebPageConfigEntity? config = await _db.WebPageConfigs.FirstOrDefaultAsync(x => x.DeletedAt == null);

        return config?.SelfEnrollmentEnabled ?? false;
    }

    [EnableRateLimiting("self-enrollment-read")]
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

    [EnableRateLimiting("self-enrollment")]
    [HttpPost]
    public async Task<ActionResult<SelfEnrollmentSubmitResult>> CreateAsync([FromBody] SelfEnrollmentRequest request)
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

        string confirmBaseUrl = $"{Request.Scheme}://{Request.Host}/enroll/confirm";
        PendingSelfEnrollmentSubmitResult result = await _pendingSelfEnrollmentService.SubmitAsync(request,
            HttpContext.GetClientIpAddress(), confirmBaseUrl);

        switch (result.Status)
        {
            case MemberCreationStatus.DUPLICATE_CONFLICT:
                Log.Warning("SelfEnrollmentController: CreateAsync -> Member or pending submission already exists");
                return Conflict(new ApiResults.ErrorResults.ConflictResult(errorCode: ApiErrorCodes.CONFLICT_RESOURCE_ALREADY_EXISTS,
                    errorMessage: "Member already exists.",
                    moreInfo: "A member or a pending self-enrollment with the same email address or IBAN already exists."));
        }

        Log.Information(
            $"SelfEnrollmentController: CreateAsync -> Submission from {request.FirstName} {request.LastName} accepted, confirmation mail sent.");
        return Created(string.Empty, new SelfEnrollmentSubmitResult());
    }

    [EnableRateLimiting("self-enrollment-confirm")]
    [HttpPost("confirm")]
    public async Task<IActionResult> ConfirmAsync([FromBody] ConfirmSelfEnrollmentRequest request)
    {
        PendingSelfEnrollmentActionStatus status = await _pendingSelfEnrollmentService.ConfirmAsync(request.Token);

        if (status == PendingSelfEnrollmentActionStatus.NOT_FOUND)
        {
            Log.Warning("SelfEnrollmentController: ConfirmAsync -> Invalid, expired or already used token");
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Confirmation link invalid or expired.",
                moreInfo: "This confirmation link is invalid, expired or was already used."));
        }

        Log.Information("SelfEnrollmentController: ConfirmAsync -> Self-enrollment confirmed.");
        return Ok();
    }
}
