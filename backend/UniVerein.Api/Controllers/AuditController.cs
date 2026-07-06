using System.Linq;
using System.Threading.Tasks;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Query;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities.Enums;
using Microsoft.AspNetCore.Cors;
using Microsoft.EntityFrameworkCore;

namespace UniVerein.Api.Controllers;

[Authorize(Roles = nameof(UserRole.ADMIN))]
[ApiController]
[Route("audit")]
[EnableCors("AllowFrontend")]
public class AuditController : ControllerBase
{
    private readonly AppDbContext _db;

    public AuditController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<AuditLogResults>> GetAsync([FromQuery] AuditLogQuery auditLogQuery)
    {
        if (auditLogQuery.Offset < 0 || auditLogQuery.Limit < 1)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Offset and/or Limit must be greater than or equal to 1."));

        IQueryable<AuditLogResult> logs = _db.AuditLogs
            .Include(c => c.User)
            .OrderByDescending(a => a.CreatedAt)
            .Select(x => new AuditLogResult()
            {
                Timestamp = x.CreatedAt,
                UserName = x.User.Username,
                Action = x.Action,
                Entity = x.Entity,
                Data = x.Data
            });

        return Ok(new AuditLogResults()
        {
            Total = await logs.CountAsync(),
            Items = await logs.Skip(auditLogQuery.Offset).Take(auditLogQuery.Limit).ToListAsync()
        });
    }
}