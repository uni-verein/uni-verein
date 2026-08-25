using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Models.Enums;
using UniVerein.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Controllers;

[Authorize]
[ApiController]
[Route("users/account/settings")]
[EnableCors("AllowFrontend")]
public class UserSettingsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuditService _auditService;
    private readonly Guid _currentUserId;

    public UserSettingsController(AppDbContext db, AuditService auditService, IHttpContextAccessor http)
    {
        _db = db;
        _auditService = auditService;

        string? userIdClaim = http.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        _currentUserId = Guid.TryParse(userIdClaim, out Guid id) ? id : Guid.Empty;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserSettingResult>>> GetAllAsync()
    {
        List<UserSettingEntity> settings = await _db.UserSettings
            .Where(s => s.UserId == _currentUserId)
            .ToListAsync();

        return Ok(settings.Select(s => new UserSettingResult
        {
            Type = s.Type,
            Enabled = s.Enabled
        }).ToList());
    }

    [HttpPut("{type}")]
    public async Task<ActionResult<UserSettingResult>> UpdateAsync([FromRoute] UserSettingType type,
        [FromBody] UpdateUserSettingRequest request)
    {
        UserSettingEntity? setting = await _db.UserSettings
            .FirstOrDefaultAsync(s => s.UserId == _currentUserId && s.Type == type);

        UserEntity? user = await _db.Users.FindAsync(_currentUserId);
        if (user == null)
            return Unauthorized();

        if (setting == null)
        {
            setting = new UserSettingEntity
            {
                UserId = _currentUserId,
                User = user,
                Type = type,
                Enabled = request.Enabled
            };
            await _db.UserSettings.AddAsync(setting);
        }
        else
        {
            setting.Enabled = request.Enabled;
        }

        await _db.SaveChangesAsync();

        await _auditService.LogAsync(AuditLogActions.UPDATE, nameof(UserSettingEntity),
            new { SettingType = type, setting.Enabled });

        return Ok(new UserSettingResult { Type = setting.Type, Enabled = setting.Enabled });
    }
}
