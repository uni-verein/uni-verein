using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using UniVerein.Api.ApiResults.Calendar;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Services.Calendar;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;

namespace UniVerein.Api.Controllers;

[AllowAnonymous]
[ApiController]
[Route("public/calendar")]
[EnableCors("AllowFrontend")]
[EnableRateLimiting("public-calendar")]
public class PublicCalendarController : ControllerBase
{
    private static readonly TimeSpan MaxQueryRange = TimeSpan.FromDays(400);
    private static readonly TimeSpan FeedPast = TimeSpan.FromDays(90);
    private static readonly TimeSpan FeedFuture = TimeSpan.FromDays(2 * 365);

    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;

    public PublicCalendarController(AppDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    [HttpGet("info")]
    public async Task<ActionResult<PublicCalendarInfoResult>> GetInfoAsync([FromQuery] string? key)
    {
        CalendarSettingsEntity settings = await CalendarPublicService.GetSettingsAsync(_db);
        if (!CalendarPublicService.IsAccessible(settings, key))
            return PublicCalendarNotFound();

        List<Guid> usedCategoryIds = await CalendarPublicService.PubliclyVisible(_db.CalendarEvents)
            .Where(e => e.CategoryId != null)
            .Select(e => e.CategoryId!.Value)
            .Distinct()
            .ToListAsync();

        List<CalendarCategoryResult> categories = await _db.CalendarCategories
            .Where(c => c.IsPublic || usedCategoryIds.Contains(c.Id))
            .OrderBy(c => c.Name)
            .Select(c => new CalendarCategoryResult { Id = c.Id, Name = c.Name, Color = c.Color, IsPublic = c.IsPublic })
            .ToListAsync();

        return Ok(new PublicCalendarInfoResult { Title = settings.PublicCalendarTitle, Categories = categories });
    }

    [HttpGet("events")]
    public async Task<ActionResult<PublicCalendarOccurrenceResults>> GetEventsAsync([FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to, [FromQuery] List<Guid>? categoryIds, [FromQuery] string? key)
    {
        CalendarSettingsEntity settings = await CalendarPublicService.GetSettingsAsync(_db);
        if (!CalendarPublicService.IsAccessible(settings, key))
            return PublicCalendarNotFound();

        from = from.ToUniversalTime();
        to = to.ToUniversalTime();
        if (to <= from || to - from > MaxQueryRange)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: $"'to' must be after 'from' and the range must not exceed {MaxQueryRange.TotalDays} days."));

        List<CalendarEventEntity> events = await PublicEvents(categoryIds)
            .Where(e => e.StartAt < to && e.SeriesEndAt > from)
            .ToListAsync();

        List<PublicCalendarOccurrenceResult> results = events
            .SelectMany(e => CalendarRecurrence.Expand(e, from, to).Select(o => new PublicCalendarOccurrenceResult
            {
                EventId = e.Id,
                Title = e.Title,
                Description = e.Description,
                Start = o.Start,
                End = o.End,
                AllDay = e.AllDay,
                LocationName = e.LocationName,
                LocationAddress = e.LocationAddress,
                LocationUrl = e.LocationUrl,
                CategoryId = e.CategoryId,
                CategoryName = e.Category?.Name,
                Color = e.Category?.Color
            }))
            .OrderBy(o => o.Start)
            .ToList();

        return Ok(new PublicCalendarOccurrenceResults { Items = results, Total = results.Count });
    }

    [HttpGet("feed.ics")]
    public async Task<IActionResult> GetFeedAsync([FromQuery] Guid? categoryId, [FromQuery] string? key)
    {
        CalendarSettingsEntity settings = await CalendarPublicService.GetSettingsAsync(_db);
        if (!CalendarPublicService.IsAccessible(settings, key))
            return PublicCalendarNotFound();

        DateTimeOffset now = _timeProvider.GetUtcNow();
        DateTimeOffset from = now - FeedPast;
        DateTimeOffset to = now + FeedFuture;

        List<CalendarEventEntity> events = await PublicEvents(categoryId != null ? [(Guid)categoryId] : null)
            .Where(e => e.StartAt < to && e.SeriesEndAt > from)
            .ToListAsync();

        string name = string.IsNullOrWhiteSpace(settings.PublicCalendarTitle)
            ? "Vereinskalender"
            : settings.PublicCalendarTitle;
        if (categoryId != null && events.FirstOrDefault()?.Category is { } category)
            name = $"{name} – {category.Name}";

        Response.Headers.CacheControl = "public, max-age=900";
        return Content(CalendarIcsBuilder.Build(name, events, now), "text/calendar; charset=utf-8");
    }

    private IQueryable<CalendarEventEntity> PublicEvents(List<Guid>? categoryIds)
    {
        IQueryable<CalendarEventEntity> events = CalendarPublicService.PubliclyVisible(_db.CalendarEvents)
            .Include(e => e.Category);

        if (categoryIds?.Count > 0)
            events = events.Where(e => e.CategoryId != null && categoryIds.Contains(e.CategoryId.Value));

        return events;
    }

    private NotFoundObjectResult PublicCalendarNotFound()
    {
        return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
            errorMessage: "Public calendar not found."));
    }
}
