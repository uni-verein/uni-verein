using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniVerein.Api.ApiResults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities.Enums;
using Microsoft.AspNetCore.Cors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using UniVerein.DAL.Entities;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using UniVerein.Api.Query;

namespace UniVerein.Api.Controllers;

[Authorize]
[ApiController]
[Route("notifications")]
[EnableCors("AllowFrontend")]
public class NotificationController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _timeProvider;
    private readonly Guid _currentUserId;

    public NotificationController(AppDbContext db, IConfiguration configuration, TimeProvider timeProvider,
        IHttpContextAccessor http)
    {
        _db = db;
        _configuration = configuration;
        _timeProvider = timeProvider;

        string? userIdClaim = http.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        _currentUserId = Guid.TryParse(userIdClaim, out Guid id) ? id : Guid.Empty;
    }

    [HttpGet]
    public async Task<ActionResult<NotificationResults>> GetAllAsync([FromQuery] QueryBase query)
    {
        IQueryable<NotificationEntity> own = _db.Notifications.Where(n => n.UserId == _currentUserId);

        List<NotificationResult> items = await own
            .OrderByDescending(n => n.CreatedAt)
            .Skip(Math.Max(query.Offset, 0))
            .Take(Math.Clamp(query.Limit, 1, 100))
            .Select(n => new NotificationResult
            {
                Id = n.Id,
                Type = n.Type,
                EventId = n.EventId,
                EventTitle = n.EventTitle,
                EventStart = n.EventStart,
                ActorUsername = n.ActorUsername,
                Details = n.Details,
                CreatedAt = n.CreatedAt,
                Read = n.ReadAt != null
            })
            .ToListAsync();

        return Ok(new NotificationResults
        {
            Items = items,
            Total = await own.CountAsync(),
            Unread = await own.CountAsync(n => n.ReadAt == null)
        });
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<NotificationUnreadCountResult>> GetUnreadCountAsync()
    {
        int unread = await _db.Notifications.CountAsync(n => n.UserId == _currentUserId && n.ReadAt == null);
        return Ok(new NotificationUnreadCountResult { Unread = unread });
    }

    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkReadAsync([FromRoute] Guid id)
    {
        NotificationEntity? notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == _currentUserId);
        if (notification == null)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(
                errorCode: Exceptions.ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Notification not found.",
                moreInfo: $"Notification with ID {id} not found."));

        notification.ReadAt ??= _timeProvider.GetUtcNow();
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllReadAsync()
    {
        List<NotificationEntity> unread = await _db.Notifications
            .Where(n => n.UserId == _currentUserId && n.ReadAt == null)
            .ToListAsync();

        DateTimeOffset now = _timeProvider.GetUtcNow();
        foreach (NotificationEntity notification in unread)
            notification.ReadAt = now;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] Guid id)
    {
        NotificationEntity? notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == _currentUserId);
        if (notification == null)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(
                errorCode: Exceptions.ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Notification not found.",
                moreInfo: $"Notification with ID {id} not found."));

        _db.Remove(notification);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("read")]
    public async Task<IActionResult> DeleteReadAsync()
    {
        List<NotificationEntity> read = await _db.Notifications
            .Where(n => n.UserId == _currentUserId && n.ReadAt != null)
            .ToListAsync();

        _db.RemoveRange(read);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpGet("firmware-update")]
    public async Task<ActionResult<FirmwareUpdateResult>> GetAsync()
    {
        List<FirmwareVersionEntity> firmwareVersions = await _db.FirmwareVersions.ToListAsync();
        if (firmwareVersions.Count == 0)
            return NoContent();

        string? currentVersionRaw = _configuration.GetValue<string>("Version");
        Version.TryParse(currentVersionRaw?.TrimStart('v'), out Version? currentVersion);

        FirmwareVersionEntity latestFirmware = firmwareVersions
            .Select(f => (Entity: f, Parsed: Version.TryParse(f.Version.TrimStart('v'), out Version? parsed) ? parsed : null))
            .Where(x => x.Parsed != null)
            .OrderByDescending(x => x.Parsed)
            .Select(x => x.Entity)
            .FirstOrDefault() ?? firmwareVersions.OrderByDescending(x => x.CreatedAt).First();

        Version.TryParse(latestFirmware.Version.TrimStart('v'), out Version? latestVersion);
        bool newFirmwareAvailable = currentVersion != null && latestVersion != null && latestVersion > currentVersion;

        return Ok(new FirmwareUpdateResult
        {
            NewFirmwareAvailable = newFirmwareAvailable,
            CurrentVersion = currentVersionRaw,
            LatestVersion = latestFirmware.Version
        });
    }
}