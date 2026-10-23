using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Serilog;
using UniVerein.Api.ApiResults.Calendar;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Services.Calendar;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Controllers;

// Personal iCal feed (webcal) of every event a user can see, including private ones. Calendar apps cannot log
// in, so the feed is protected by a secret token in the URL that the user can revoke at any time.
[Authorize]
[ApiController]
[Route("calendar/feed")]
[EnableCors("AllowFrontend")]
public class CalendarFeedController : ControllerBase
{
    private static readonly TimeSpan FeedPast = TimeSpan.FromDays(180);
    private static readonly TimeSpan FeedFuture = TimeSpan.FromDays(2 * 365);

    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly Guid _currentUserId;

    public CalendarFeedController(AppDbContext db, TimeProvider timeProvider, IHttpContextAccessor http)
    {
        _db = db;
        _timeProvider = timeProvider;

        string? userIdClaim = http.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        _currentUserId = Guid.TryParse(userIdClaim, out Guid id) ? id : Guid.Empty;
    }

    [HttpGet("token")]
    public async Task<ActionResult<CalendarFeedStatusResult>> GetStatusAsync()
    {
        CalendarFeedTokenEntity? token = await _db.CalendarFeedTokens
            .FirstOrDefaultAsync(t => t.UserId == _currentUserId && t.DeletedAt == null);

        return Ok(new CalendarFeedStatusResult
        {
            Exists = token != null,
            CreatedAt = token?.CreatedAt,
            LastUsedAt = token?.LastUsedAt
        });
    }

    [HttpPost("token")]
    public async Task<ActionResult<CalendarFeedTokenResult>> CreateTokenAsync()
    {
        await RevokeTokensAsync();

        string token = CalendarPublicService.NewFeedToken();
        await _db.CalendarFeedTokens.AddAsync(new CalendarFeedTokenEntity
        {
            UserId = _currentUserId,
            TokenHash = CalendarPublicService.HashToken(token)
        });
        await _db.SaveChangesAsync();

        Log.Information($"CalendarFeedController: CreateTokenAsync -> New calendar feed token for user {_currentUserId}.");
        return Ok(new CalendarFeedTokenResult { Token = token, Path = $"/calendar/feed/{token}.ics" });
    }

    [HttpDelete("token")]
    public async Task<IActionResult> DeleteTokenAsync()
    {
        await RevokeTokensAsync();
        await _db.SaveChangesAsync();

        Log.Information($"CalendarFeedController: DeleteTokenAsync -> Calendar feed of user {_currentUserId} revoked.");
        return NoContent();
    }

    [AllowAnonymous]
    [EnableRateLimiting("calendar-feed")]
    [HttpGet("{token}.ics")]
    public async Task<IActionResult> GetFeedAsync([FromRoute] string token)
    {
        string hash = CalendarPublicService.HashToken(token);
        CalendarFeedTokenEntity? feedToken = await _db.CalendarFeedTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.DeletedAt == null);
        UserEntity? user = feedToken == null ? null : await _db.Users.FindAsync(feedToken.UserId);

        if (feedToken == null || user == null || user.DeletedAt != null)
        {
            Log.Warning("CalendarFeedController: GetFeedAsync -> Invalid, revoked or orphaned calendar feed token used.");
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Calendar feed not found."));
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        DateTimeOffset from = now - FeedPast;
        DateTimeOffset to = now + FeedFuture;

        List<CalendarEventEntity> events = await CalendarService
            .VisibleTo(_db.CalendarEvents, user.Id, user.Role == UserRole.ADMIN)
            .Include(e => e.Category)
            .Where(e => e.StartAt < to && e.SeriesEndAt > from)
            .ToListAsync();

        feedToken.LastUsedAt = now;
        await _db.SaveChangesAsync();

        string? baseUrl = await _db.WebPageConfigs.Select(c => c.PublicBaseUrl).FirstOrDefaultAsync();
        string ics = CalendarIcsBuilder.Build("Uni-Verein", events, now,
            e => string.IsNullOrWhiteSpace(baseUrl) ? null : $"{baseUrl.TrimEnd('/')}/?page=calendar&event={e.Id}");

        Response.Headers.CacheControl = "private, max-age=300";
        return Content(ics, "text/calendar; charset=utf-8");
    }

    private async Task RevokeTokensAsync()
    {
        List<CalendarFeedTokenEntity> tokens = await _db.CalendarFeedTokens
            .Where(t => t.UserId == _currentUserId && t.DeletedAt == null)
            .ToListAsync();
        _db.RemoveRange(tokens);
    }
}
