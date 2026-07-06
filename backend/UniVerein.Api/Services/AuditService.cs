using System;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using UniVerein.Api.Data.Enums;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace UniVerein.Api.Services;

public class AuditService
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AuditService(AppDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public async Task LogAsync(AuditLogActions action, string entity, object? data)
    {
        var userIdClaim = _http.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return;

        UserEntity? user = await  _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return;

        await _db.AuditLogs.AddAsync(new AuditLogEntity()
        {
            UserId = user.Id,
            User = user,
            Action = action.ToString(),
            Entity = entity,
            Data = data is null ? string.Empty : JsonSerializer.Serialize(data, _jsonOptions),
        });

        await _db.SaveChangesAsync();
    }
}