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
        FirmwareVersionEntity? firmware = await _db.FirmwareVersions.OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync();
        if (firmware == null)
            return NoContent();
        
        string? currentVersion = _configuration.GetValue<string>("Version");

        FirmwareUpdateResult firmwareUpdateResult = new()
        {
            NewFirmwareAvailable = false,
            CurrentVersion = currentVersion,
            LatestVersion = firmware?.Version
        };
        
        if (firmware!.TagName.Equals(currentVersion))
            return Ok(firmwareUpdateResult);
        
        firmwareUpdateResult.NewFirmwareAvailable = true;
        return Ok(firmwareUpdateResult);
    }
}