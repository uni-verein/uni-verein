using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniVerein.Api.ApiResults.Calendar;
using UniVerein.DAL.Data;

namespace UniVerein.Api.Controllers;

// Minimal user directory (id + username) so every user can invite others to calendar events. The full user
// list under /users stays admin-only because it contains e-mail addresses and roles.
[Authorize]
[ApiController]
[Route("calendar/users")]
[EnableCors("AllowFrontend")]
public class CalendarUsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public CalendarUsersController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<CalendarUserResults>> GetAllAsync()
    {
        List<CalendarUserResult> results = await _db.Users
            .OrderBy(u => u.Username)
            .Select(u => new CalendarUserResult { Id = u.Id, Username = u.Username })
            .ToListAsync();

        return Ok(new CalendarUserResults { Items = results, Total = results.Count });
    }
}
