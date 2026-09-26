using System;
using System.Net;
using System.Threading.Tasks;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniVerein.Api.Services;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace UniVerein.Api.Controllers;

[ApiController]
[Route("auth")]
[EnableCors("AllowFrontend")]
public class AuthController : ControllerBase
{
    private static readonly string DummyPasswordHash = CryptoService.HashPassword(Guid.NewGuid().ToString());

    private readonly AppDbContext _db;
    private readonly JwtService _jwt;
    private readonly TimeProvider _timeProvider;

    public AuthController(AppDbContext db, JwtService jwt, TimeProvider timeProvider)
    {
        _db = db;
        _jwt = jwt;
        _timeProvider = timeProvider;
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    [HttpPost("login")]
    public async Task<ActionResult<LoginApiResult>> LoginAsync([FromBody] LoginRequest request)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();

        UserEntity? user = await _db.Users.FirstOrDefaultAsync(x => x.Username == request.Username);
        if (user == null)
        {
            // Pay the same Argon2id cost as a real login attempt so the response time
            // does not reveal whether the username exists.
            try
            {
                CryptoService.VerifyPassword(request.Password, DummyPasswordHash);
            }
            catch (Exception)
            {
                // ignored: only used to equalize timing
            }

            Log.Warning("AuthController: Login failed, user not found.");
            return Unauthorized();
        }

        if (user.BlockingLoginTimeout.HasValue && user.BlockingLoginTimeout > now)
        {
            Log.Warning("AuthController: Login blocked by timeout.");
            TimeSpan remaining = user.BlockingLoginTimeout.Value - now;
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
                user.FailedAttempts = LoginGuard.GetEffectiveFailedAttempts(user.FailedAttempts, user.LastFailedLoginAttempt, now) + 1;
                user.LastFailedLoginAttempt = now;
                user.BlockingLoginTimeout = LoginGuard.GetLockoutReleaseTime(user.FailedAttempts, now);
                _db.Users.Update(user);
                await _db.SaveChangesAsync();
                return Unauthorized();
            }
        }
        catch (Exception)
        {
            Log.Warning("AuthController: Password validation threw an exception.");
            return Unauthorized();
        }

        user.FailedAttempts = 0;
        user.LastFailedLoginAttempt = null;
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