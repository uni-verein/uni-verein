using System;
using System.Net;
using System.Threading.Tasks;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Security;
using Microsoft.AspNetCore.Mvc;
using UniVerein.Api.Services;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using Microsoft.AspNetCore.Cors;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace UniVerein.Api.Controllers;

[ApiController]
[Route("auth")]
[EnableCors("AllowFrontend")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtService _jwt;

    public AuthController(AppDbContext db, JwtService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request)
    {
        UserEntity? user = await _db.Users.FirstOrDefaultAsync(x => x.Username == request.Username);
        if (user == null)
        {
            Log.Warning($"AuthController: User not found, UserName: {request.Username}");
            return Unauthorized();
        }

        if (user.BlockingLoginTimeout.HasValue && user.BlockingLoginTimeout > DateTime.UtcNow)
        {
            Log.Warning($"AuthController: User blocked by timeout, UserName: {request.Username}");
            TimeSpan remaining = user.BlockingLoginTimeout.Value - DateTime.UtcNow;
            return StatusCode((int)HttpStatusCode.Forbidden, new LoginApiBlockedResult()
            {
                Error = $"To many login attempts.",
                RemainingTime = remaining.TotalSeconds
            });
        }

        try
        {
            if (!CryptoService.VerifyPassword(request.Password, user.PasswordHash))
            {
                user.FailedAttempts++;
                user.BlockingLoginTimeout = LoginGuard.GetLockoutReleaseTime(user.FailedAttempts);
                _db.Users.Update(user);
                await _db.SaveChangesAsync();
                return Unauthorized();
            }
        }
        catch (Exception)
        {
            Log.Warning($"AuthController: Password validation failed, UserName: {request.Username}");
            return Unauthorized();
        }

        user.FailedAttempts = 0;
        user.BlockingLoginTimeout = null;

        _db.Users.Update(user);
        await _db.SaveChangesAsync();
        LoginApiResult result = new()
        {
            Token = _jwt.CreateToken(user),
        };

        return Ok(result);
    }
}