using System;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Extensions;
using UniVerein.Api.Models.Enums;
using UniVerein.Api.Security;
using UniVerein.Api.Services;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Controllers;

[ApiController]
[Route("users")]
[EnableCors("AllowFrontend")]
[DisabledInDemo]
public class TwoFactorController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TwoFactorService _twoFactor;
    private readonly AuditService _auditService;
    private readonly LoginAttemptTracker _attempts;
    private readonly JwtService _jwt;

    public TwoFactorController(AppDbContext db, TwoFactorService twoFactor, AuditService auditService, LoginAttemptTracker attempts,
        JwtService jwt)
    {
        _db = db;
        _twoFactor = twoFactor;
        _auditService = auditService;
        _attempts = attempts;
        _jwt = jwt;
    }

    [Authorize]
    [HttpPost("account/2fa/setup")]
    public async Task<ActionResult<TwoFactorSetupApiResult>> SetupAsync([FromBody] TwoFactorPasswordRequest request)
    {
        UserEntity? user = await GetCurrentUserAsync();
        if (user == null)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "User not valid."));

        if (user.TotpEnabled)
            return Conflict(new ApiResults.ErrorResults.ConflictResult(errorCode: ApiErrorCodes.CONFLICT_RESOURCE_ALREADY_EXISTS,
                errorMessage: "Two-factor authentication is already enabled.",
                moreInfo: "Disable two-factor authentication before setting it up again."));

        ActionResult? blocked = CheckLockout(LoginAttemptScope.TwoFactorSetup, user);
        if (blocked != null)
            return blocked;

        if (!IsPasswordCorrect(user, request.Password))
        {
            RegisterFailure(LoginAttemptScope.TwoFactorSetup, user);
            Log.Warning("TwoFactorController: SetupAsync -> Wrong password for user {UserId}.", user.Id);
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Password is incorrect."));
        }

        ResetFailures(LoginAttemptScope.TwoFactorSetup, user);

        string secret = TwoFactorService.GenerateSecret();
        user.TotpSecret = _twoFactor.EncryptSecret(secret);
        user.TotpLastUsedStep = null;
        await _db.SaveChangesAsync();

        return Ok(new TwoFactorSetupApiResult
        {
            Secret = secret,
            OtpAuthUri = _twoFactor.BuildOtpAuthUri(user.Username, secret)
        });
    }

    [Authorize]
    [HttpPost("account/2fa/enable")]
    public async Task<ActionResult<TwoFactorEnabledApiResult>> EnableAsync([FromBody] TwoFactorCodeRequest request)
    {
        UserEntity? user = await GetCurrentUserAsync();
        if (user == null)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "User not valid."));

        if (user.TotpEnabled || string.IsNullOrEmpty(user.TotpSecret))
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Start the two-factor setup first."));

        ActionResult? blocked = CheckLockout(LoginAttemptScope.TwoFactorEnable, user);
        if (blocked != null)
            return blocked;

        string secret = _twoFactor.DecryptSecret(user.TotpSecret)!;
        if (!_twoFactor.TryVerifyCode(secret, request.Code, null, out long step))
        {
            RegisterFailure(LoginAttemptScope.TwoFactorEnable, user);
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "The code is incorrect."));
        }

        System.Collections.Generic.List<string> plain = await _twoFactor.ReplaceRecoveryCodesAsync(user.Id);
        user.TotpEnabled = true;
        user.TotpLastUsedStep = step;
        user.SessionVersion++;
        await _db.SaveChangesAsync();
        _jwt.IssueSession(HttpContext, user);
        ResetFailures(LoginAttemptScope.TwoFactorEnable, user);

        await _auditService.LogAsync(AuditLogActions.TWO_FACTOR_ENABLE, nameof(UserEntity), new { UserId = user.Id });
        Log.Information("TwoFactorController: EnableAsync -> User {UserId} enabled two-factor authentication.", user.Id);
        return Ok(new TwoFactorEnabledApiResult { RecoveryCodes = plain });
    }

    [Authorize]
    [HttpPost("account/2fa/disable")]
    public async Task<IActionResult> DisableAsync([FromBody] TwoFactorDisableRequest request)
    {
        UserEntity? user = await GetCurrentUserAsync();
        if (user == null)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "User not valid."));

        if (!user.TotpEnabled || string.IsNullOrEmpty(user.TotpSecret))
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Two-factor authentication is not enabled."));

        ActionResult? blocked = CheckLockout(LoginAttemptScope.TwoFactorDisable, user);
        if (blocked != null)
            return blocked;

        bool passwordOk = IsPasswordCorrect(user, request.Password);

        string secret = _twoFactor.DecryptSecret(user.TotpSecret)!;
        bool codeOk = _twoFactor.TryVerifyCode(secret, request.Code, user.TotpLastUsedStep, out _)
                      || await _twoFactor.TryConsumeRecoveryCodeAsync(user.Id, request.Code);
        if (!passwordOk || !codeOk)
        {
            RegisterFailure(LoginAttemptScope.TwoFactorDisable, user);
            Log.Warning("TwoFactorController: DisableAsync -> Wrong password or code for user {UserId}.", user.Id);
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Password or code is incorrect."));
        }

        await ClearTwoFactorAsync(user);
        _jwt.IssueSession(HttpContext, user);
        ResetFailures(LoginAttemptScope.TwoFactorDisable, user);

        await _auditService.LogAsync(AuditLogActions.TWO_FACTOR_DISABLE, nameof(UserEntity), new { UserId = user.Id });
        Log.Information("TwoFactorController: DisableAsync -> User {UserId} disabled two-factor authentication.", user.Id);
        return NoContent();
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpDelete("{id}/2fa")]
    public async Task<IActionResult> ResetAsync([FromRoute] Guid id)
    {
        if (id == HttpContext.GetActorId())
            return StatusCode((int)HttpStatusCode.Forbidden, new ApiResults.ErrorResults.ForbiddenRequestResult(
                errorMessage: "Use the account settings to disable your own two-factor authentication.",
                moreInfo: "Admins cannot reset their own two-factor authentication here."));

        UserEntity? user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (user == null)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "User not found.",
                moreInfo: $"User with ID {id} not found."));

        await ClearTwoFactorAsync(user);

        await _auditService.LogAsync(AuditLogActions.TWO_FACTOR_RESET, nameof(UserEntity), new { UserId = user.Id, user.Username });
        Log.Information("TwoFactorController: ResetAsync -> Two-factor authentication of user {UserId} reset by {ActorId}.",
            user.Id, HttpContext.GetActorId());
        return NoContent();
    }

    private static bool IsPasswordCorrect(UserEntity user, string password)
    {
        try
        {
            return CryptoService.VerifyPassword(password, user.PasswordHash);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task ClearTwoFactorAsync(UserEntity user)
    {
        user.TotpEnabled = false;
        user.TotpSecret = null;
        user.TotpLastUsedStep = null;
        user.SessionVersion++;
        await _db.SaveChangesAsync();
        await _twoFactor.DeleteRecoveryCodesAsync(user.Id);
    }

    private void RegisterFailure(LoginAttemptScope scope, UserEntity user)
    {
        _attempts.RegisterFailure(user.Id.ToString(), HttpContext.GetClientIpAddress(), scope);
    }

    private void ResetFailures(LoginAttemptScope scope, UserEntity user)
    {
        _attempts.Reset(user.Id.ToString(), HttpContext.GetClientIpAddress(), scope);
    }

    private ActionResult? CheckLockout(LoginAttemptScope scope, UserEntity user)
    {
        TimeSpan? remaining = _attempts.GetRemainingLockout(user.Id.ToString(), HttpContext.GetClientIpAddress(), scope);
        if (!remaining.HasValue)
            return null;

        return StatusCode((int)HttpStatusCode.Forbidden, new LoginApiBlockedResult()
        {
            Error = "To many attempts.",
            RemainingTime = remaining.Value.TotalSeconds
        });
    }

    private async Task<UserEntity?> GetCurrentUserAsync()
    {
        string? claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out Guid id) ? await _db.Users.FirstOrDefaultAsync(x => x.Id == id) : null;
    }
}
