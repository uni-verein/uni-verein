using System;
using System.Net;
using System.Threading.Tasks;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Extensions;
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
    private readonly LoginAttemptTracker _loginAttempts;

    public AuthController(AppDbContext db, JwtService jwt, LoginAttemptTracker loginAttempts)
    {
        _db = db;
        _jwt = jwt;
        _loginAttempts = loginAttempts;
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    [HttpPost("login")]
    public async Task<ActionResult<LoginApiResult>> LoginAsync([FromBody] LoginRequest request)
    {
        string clientIp = HttpContext.GetClientIpAddress();

        TimeSpan? remaining = _loginAttempts.GetRemainingLockout(request.Username, clientIp);
        if (remaining.HasValue)
        {
            Log.Warning("AuthController: LoginAsync -> Login blocked by lockout, {RemainingSeconds} seconds remaining.",
                (int)remaining.Value.TotalSeconds);
            return StatusCode((int)HttpStatusCode.Forbidden, new LoginApiBlockedResult()
            {
                Error = $"To many login attempts.",
                RemainingTime = remaining.Value.TotalSeconds
            });
        }

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

            _loginAttempts.RegisterFailure(request.Username, clientIp);
            Log.Warning("AuthController: Login failed, user not found.");
            return Unauthorized();
        }

        try
        {
            if (!CryptoService.VerifyPassword(request.Password, user.PasswordHash))
            {
                _loginAttempts.RegisterFailure(request.Username, clientIp);
                Log.Warning("AuthController: LoginAsync -> Wrong password for user {UserId}.", user.Id);
                return Unauthorized();
            }
        }
        catch (Exception ex)
        {
            Log.Warning("AuthController: LoginAsync -> Password validation for user {UserId} threw {ExceptionType}.",
                user.Id, ex.GetType().Name);
            return Unauthorized();
        }

        _loginAttempts.Reset(request.Username, clientIp);
        user.FailedAttempts = 0;
        user.LastFailedLoginAttempt = null;
        user.BlockingLoginTimeout = null;

        _db.Users.Update(user);
        await _db.SaveChangesAsync();
        Log.Information("AuthController: LoginAsync -> User {UserId} logged in with role {Role}.", user.Id, user.Role);
        LoginApiResult result = new()
        {
            Token = _jwt.CreateToken(user),
        };

        return Ok(result);
    }
}