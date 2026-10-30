using System;
using System.Linq;
using System.Net;
using System.Security.Claims;
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
    private readonly TwoFactorService _twoFactor;
    private readonly AccountLockoutTracker _accountLockout;

    public AuthController(AppDbContext db, JwtService jwt, LoginAttemptTracker loginAttempts, TwoFactorService twoFactor,
        AccountLockoutTracker accountLockout)
    {
        _db = db;
        _jwt = jwt;
        _loginAttempts = loginAttempts;
        _twoFactor = twoFactor;
        _accountLockout = accountLockout;
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    [HttpPost("login")]
    public async Task<ActionResult> LoginAsync([FromBody] LoginRequest request)
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

        if (user.TotpEnabled)
        {
            Log.Information("AuthController: LoginAsync -> User {UserId} passed the password check, two-factor code required.", user.Id);
            return Ok(new LoginTwoFactorRequiredApiResult { Challenge = _twoFactor.CreateChallenge(user) });
        }

        return CompleteLogin(user);
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    [HttpPost("login/2fa")]
    public async Task<IActionResult> LoginTwoFactorAsync([FromBody] TwoFactorLoginRequest request)
    {
        string clientIp = HttpContext.GetClientIpAddress();

        (Guid UserId, int SessionVersion)? challenge = _twoFactor.ValidateChallenge(request.Challenge);
        UserEntity? user = challenge.HasValue
            ? await _db.Users.FirstOrDefaultAsync(x => x.Id == challenge.Value.UserId)
            : null;

        if (user == null || user.SessionVersion != challenge!.Value.SessionVersion ||
            !user.TotpEnabled || string.IsNullOrEmpty(user.TotpSecret))
        {
            Log.Warning("AuthController: LoginTwoFactorAsync -> Invalid or expired challenge.");
            return Unauthorized();
        }

        string trackerSubject = user.Id.ToString();
        bool isCodeGuess = TwoFactorService.IsTotpFormat(request.Code);
        TimeSpan? addressLock = _loginAttempts.GetRemainingLockout(trackerSubject, clientIp, LoginAttemptScope.TwoFactorLogin);
        if (addressLock.HasValue)
        {
            TimeSpan? accountLock = isCodeGuess ? _accountLockout.GetRemainingLockout(user.Id) : null;
            return TwoFactorBlocked(accountLock > addressLock ? accountLock.Value : addressLock.Value, recoveryCodeAllowed: false);
        }

        TimeSpan? startedBlock = null;
        TimeSpan? reservationLock = isCodeGuess ? _accountLockout.ReserveAttempt(user.Id, out startedBlock) : null;
        if (reservationLock.HasValue)
            return TwoFactorBlocked(reservationLock.Value, recoveryCodeAllowed: true);

        string secret = _twoFactor.DecryptSecret(user.TotpSecret)!;
        if (_twoFactor.TryVerifyCode(secret, request.Code, user.TotpLastUsedStep, out long step))
        {
            user.TotpLastUsedStep = step;
        }
        else
        {
            if (!await _twoFactor.TryConsumeRecoveryCodeAsync(user.Id, request.Code))
            {
                _loginAttempts.RegisterFailure(trackerSubject, clientIp, LoginAttemptScope.TwoFactorLogin);
                Log.Warning("AuthController: LoginTwoFactorAsync -> Wrong code for user {UserId}.", user.Id);
                if (startedBlock.HasValue)
                    Log.Warning("AuthController: LoginTwoFactorAsync -> Two-factor login of user {UserId} blocked for {BlockSeconds} seconds after repeated wrong codes.",
                        user.Id, (int)startedBlock.Value.TotalSeconds);

                return Unauthorized();
            }

            Log.Information("AuthController: LoginTwoFactorAsync -> User {UserId} used a recovery code.", user.Id);
        }

        _loginAttempts.Reset(trackerSubject, clientIp, LoginAttemptScope.TwoFactorLogin);
        _accountLockout.Reset(user.Id);
        _db.Users.Update(user);
        await _db.SaveChangesAsync();
        return CompleteLogin(user);
    }

    private ObjectResult TwoFactorBlocked(TimeSpan remaining, bool recoveryCodeAllowed)
    {
        Log.Warning("AuthController: LoginTwoFactorAsync -> Blocked by lockout, {RemainingSeconds} seconds remaining.",
            (int)remaining.TotalSeconds);
        return StatusCode((int)HttpStatusCode.Forbidden, new LoginApiBlockedResult()
        {
            Error = "To many login attempts.",
            RemainingTime = remaining.TotalSeconds,
            RecoveryCodeAllowed = recoveryCodeAllowed
        });
    }

    private ActionResult CompleteLogin(UserEntity user)
    {
        Log.Information("AuthController: LoginAsync -> User {UserId} logged in with role {Role}.", user.Id, user.Role);
        string token = _jwt.IssueSession(HttpContext, user);

        return Ok(new LoginApiResult
        {
            Token = token,
        });
    }

    [Authorize]
    [HttpGet("me")]
    public ActionResult<AuthSessionApiResult> GetSession()
    {
        return Ok(new AuthSessionApiResult
        {
            Id = HttpContext.GetActorId(),
            Name = User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty,
            Role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty
        });
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> LogoutAsync()
    {
        Guid userId = HttpContext.GetActorId();
        if (userId != Guid.Empty)
        {
            await _db.Users.Where(x => x.Id == userId)
                .ExecuteUpdateAsync(x => x.SetProperty(u => u.SessionVersion, u => u.SessionVersion + 1));
            Log.Information("AuthController: LogoutAsync -> User {UserId} logged out, all sessions revoked.", userId);
        }

        AuthCookie.Delete(HttpContext);
        return NoContent();
    }
}