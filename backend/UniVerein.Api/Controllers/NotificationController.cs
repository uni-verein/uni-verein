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

namespace UniVerein.Api.Controllers;

[Authorize(Roles = nameof(UserRole.ADMIN))]
[ApiController]
[Route("notifications")]
[EnableCors("AllowFrontend")]
public class NotificationController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    public NotificationController(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

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