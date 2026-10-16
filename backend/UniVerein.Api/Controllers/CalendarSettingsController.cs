using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults.Calendar;
using UniVerein.Api.Models.Enums;
using UniVerein.Api.Services;
using UniVerein.Api.Services.Calendar;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Controllers;

[Authorize]
[ApiController]
[Route("calendar/settings")]
[EnableCors("AllowFrontend")]
public class CalendarSettingsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuditService _auditService;

    public CalendarSettingsController(AppDbContext db, AuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    // Readable for every user, so members can share the link of the public calendar.
    [HttpGet]
    public async Task<ActionResult<CalendarSettingsResult>> GetAsync()
    {
        return Ok(ToResult(await CalendarPublicService.GetSettingsAsync(_db)));
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpPut]
    public async Task<ActionResult<CalendarSettingsResult>> UpdateAsync([FromBody] CalendarSettingsRequest request)
    {
        CalendarSettingsEntity? settings = await _db.CalendarSettings.FirstOrDefaultAsync(s => s.DeletedAt == null);
        if (settings == null)
        {
            settings = new CalendarSettingsEntity();
            await _db.CalendarSettings.AddAsync(settings);
        }

        settings.PublicCalendarEnabled = request.PublicCalendarEnabled;
        settings.PublicCalendarTitle = request.PublicCalendarTitle?.Trim() ?? string.Empty;
        if (!request.RequireKey)
            settings.PublicCalendarKey = null;
        else if (request.RegenerateKey || string.IsNullOrEmpty(settings.PublicCalendarKey))
            settings.PublicCalendarKey = CalendarPublicService.NewKey();

        await _db.SaveChangesAsync();
        await _auditService.LogAsync(AuditLogActions.UPDATE, nameof(CalendarSettingsEntity), new
        {
            settings.PublicCalendarEnabled,
            RequireKey = settings.PublicCalendarKey != null,
            request.RegenerateKey
        });
        Log.Information($"CalendarSettingsController: UpdateAsync -> Public calendar enabled: {settings.PublicCalendarEnabled}.");

        return Ok(ToResult(settings));
    }

    private static CalendarSettingsResult ToResult(CalendarSettingsEntity settings)
    {
        return new CalendarSettingsResult
        {
            PublicCalendarEnabled = settings.PublicCalendarEnabled,
            PublicCalendarTitle = settings.PublicCalendarTitle,
            RequireKey = settings.PublicCalendarKey != null,
            PublicCalendarKey = settings.PublicCalendarKey
        };
    }
}
