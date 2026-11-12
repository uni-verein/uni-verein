using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Extensions;
using UniVerein.Api.Helper;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Controllers;

[ApiController]
[Route("enroll-form-settings")]
[Authorize(Roles = nameof(UserRole.ADMIN))]
public class EnrollFormSettingsController : ControllerBase
{
    private readonly AppDbContext _db;

    public EnrollFormSettingsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<EnrollFormSettingsResult>> GetAsync()
    {
        EnrollFormSettingsEntity? settings = await _db.EnrollFormSettings.FirstOrDefaultAsync();

        return Ok(ToResult(settings));
    }

    [HttpPut]
    public async Task<ActionResult<EnrollFormSettingsResult>> UpdateAsync([FromBody] EnrollFormSettingsRequest request)
    {
        List<string> origins = EmbedOrigins.Parse(request.EmbedAllowedOrigins);

        if (origins.Count > EnrollFormSettingsLimits.MaxEmbedOrigins)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: $"At most {EnrollFormSettingsLimits.MaxEmbedOrigins} origins are allowed."));

        string? invalid = origins.FirstOrDefault(x => !EmbedOrigins.IsValid(x));
        if (invalid != null)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: "Origins must look like https://example.org, without a path."));

        WebPageConfigEntity? webPageConfig = null;
        if (request.SelfEnrollmentEnabled)
        {
            bool mailConfigured = await _db.MailSettings.AnyAsync(x => x.DeletedAt == null);
            if (!mailConfigured)
                return UnprocessableEntity(new ApiResults.ErrorResults.UnprocessableEntityResult(
                    errorCode: ApiErrorCodes.UNPROCESSABLE_ENTITY,
                    errorMessage: "Mail settings required.",
                    moreInfo: "Self-enrollment requires mail settings to be configured first, " +
                              "so confirmation and notification emails can be sent."));

            webPageConfig = await _db.WebPageConfigs.FirstOrDefaultAsync(x => x.DeletedAt == null);
            if (webPageConfig == null)
                return UnprocessableEntity(new ApiResults.ErrorResults.UnprocessableEntityResult(
                    errorCode: ApiErrorCodes.UNPROCESSABLE_ENTITY,
                    errorMessage: "Club name required.",
                    moreInfo: "Self-enrollment requires the web page settings (club name) to be saved first."));
        }

        string legalText = MailHtmlSanitizer.Sanitize(request.LegalText);
        if (!MailHtmlSanitizer.HasVisibleContent(legalText))
            legalText = string.Empty;
        string embedAllowedOrigins = string.Join('\n', origins);

        string? publicBaseUrl = request.SelfEnrollmentEnabled ? $"{Request.Scheme}://{Request.Host}" : null;

        EnrollFormSettingsEntity? settings = await _db.EnrollFormSettings.FirstOrDefaultAsync();
        bool isNew = settings == null;
        if (settings == null)
        {
            settings = new EnrollFormSettingsEntity();
            await _db.EnrollFormSettings.AddAsync(settings);
        }

        ApplyChanges(settings, webPageConfig, request.SelfEnrollmentEnabled, legalText, embedAllowedOrigins, publicBaseUrl);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException) when (isNew)
        {
            _db.ChangeTracker.Clear();
            settings = await _db.EnrollFormSettings.FirstOrDefaultAsync();
            if (settings == null)
                throw;

            webPageConfig = request.SelfEnrollmentEnabled
                ? await _db.WebPageConfigs.FirstOrDefaultAsync(x => x.DeletedAt == null)
                : null;
            ApplyChanges(settings, webPageConfig, request.SelfEnrollmentEnabled, legalText, embedAllowedOrigins, publicBaseUrl);
            await _db.SaveChangesAsync();
        }

        Log.Information("EnrollFormSettingsController: UpdateAsync -> Enroll form settings {SettingsId} updated by {ActorId}, self-enrollment enabled: {SelfEnrollmentEnabled}.",
            settings.Id, HttpContext.GetActorId(), settings.SelfEnrollmentEnabled);

        return Ok(ToResult(settings));
    }

    private static void ApplyChanges(EnrollFormSettingsEntity settings, WebPageConfigEntity? webPageConfig,
        bool enabled, string legalText, string embedAllowedOrigins, string? publicBaseUrl)
    {
        settings.SelfEnrollmentEnabled = enabled;
        settings.LegalText = legalText;
        settings.EmbedAllowedOrigins = embedAllowedOrigins;

        if (webPageConfig != null && publicBaseUrl != null)
            webPageConfig.PublicBaseUrl = publicBaseUrl;
    }

    private static EnrollFormSettingsResult ToResult(EnrollFormSettingsEntity? settings)
    {
        return new EnrollFormSettingsResult
        {
            SelfEnrollmentEnabled = settings?.SelfEnrollmentEnabled ?? false,
            LegalText = settings?.LegalText ?? string.Empty,
            EmbedAllowedOrigins = settings?.EmbedAllowedOrigins ?? string.Empty
        };
    }
}
