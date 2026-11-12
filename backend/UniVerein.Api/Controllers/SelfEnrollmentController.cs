using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Extensions;
using UniVerein.Api.Helper;
using UniVerein.Api.Models;
using UniVerein.Api.Models.Enums;
using UniVerein.Api.Services;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
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
[AllowAnonymous]
public class SelfEnrollmentController : ControllerBase
{
    private const int MaxRequestSizeBytes = 32 * 1024;

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

    private async Task<EnrollFormSettingsEntity?> GetEnabledSettingsAsync()
    {
        EnrollFormSettingsEntity? settings = await _db.EnrollFormSettings.FirstOrDefaultAsync();

        return settings is { SelfEnrollmentEnabled: true } ? settings : null;
    }

    [EnableRateLimiting("self-enrollment-read")]
    [HttpGet("form-data")]
    public async Task<ActionResult<SelfEnrollmentFormDataResult>> GetFormDataAsync()
    {
        SelfEnrollmentFormDataResult result = new();
        EnrollFormSettingsEntity? settings = await GetEnabledSettingsAsync();
        if (settings == null)
            return Ok(result);

        result.Enabled = true;
        result.LegalText = EnrollLegalText.Render(settings.LegalText);
        result.MemberCategories = await _referenceDataService.GetMemberCategoriesAsync();
        result.ContributionPlans = await _referenceDataService.GetContributionPlansAsync();

        return Ok(result);
    }

    [EnableRateLimiting("self-enrollment-embed-policy")]
    [HttpGet("embed-policy")]
    public async Task<IActionResult> GetEmbedPolicyAsync()
    {
        string ancestors = "'none'";

        EnrollFormSettingsEntity? settings = await GetEnabledSettingsAsync();
        if (settings != null)
        {
            List<string> origins = EmbedOrigins.ParseValid(settings.EmbedAllowedOrigins);
            if (origins.Count > 0)
                ancestors = string.Join(' ', origins);
        }

        Response.Headers["X-Embed-Frame-Ancestors"] = ancestors;
        Response.Headers.CacheControl = "no-store";
        return NoContent();
    }

    [RequestSizeLimit(MaxRequestSizeBytes)]
    [EnableRateLimiting("self-enrollment")]
    [HttpPost]
    public async Task<ActionResult<SelfEnrollmentSubmitResult>> CreateAsync([FromBody] SelfEnrollmentRequest request)
    {
        Log.Information("SelfEnrollmentController: CreateAsync -> Received a self-enrollment submission.");

        EnrollFormSettingsEntity? settings = await GetEnabledSettingsAsync();
        if (settings == null)
        {
            Log.Warning("SelfEnrollmentController: CreateAsync -> Self-enrollment is disabled");
            return StatusCode(StatusCodes.Status403Forbidden, new ApiResults.ErrorResults.ForbiddenRequestResult(
                errorMessage: "Self-enrollment is disabled.",
                moreInfo: "Self-enrollment is currently not enabled for this club."));
        }

        WebPageConfigEntity? config = await _db.WebPageConfigs.FirstOrDefaultAsync(x => x.DeletedAt == null);
        if (string.IsNullOrEmpty(config?.PublicBaseUrl))
        {
            Log.Error(
                "SelfEnrollmentController: CreateAsync -> Self-enrollment is enabled but no public base URL is configured. " +
                "An admin must re-save the web page config to capture it.");
            throw new BaseHttpException(ApiErrorCodes.INTERNAL_SERVER_ERROR, HttpStatusCode.InternalServerError,
                errorMessage: "Self-enrollment is misconfigured.",
                moreInfo: "No public base URL is configured for this club. An admin must re-save the web page settings.");
        }

        bool legalRequired = EnrollLegalText.Render(settings.LegalText).Length > 0;
        if (legalRequired && !request.LegalAccepted)
        {
            Log.Warning("SelfEnrollmentController: CreateAsync -> Legal information was not accepted");
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: "The legal information must be accepted."));
        }

        string confirmBaseUrl = $"{config.PublicBaseUrl.TrimEnd('/')}/enroll/confirm";
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

        Log.Information("SelfEnrollmentController: CreateAsync -> Submission accepted, status {Status}.", result.Status);
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
