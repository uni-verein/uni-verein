using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Extensions;
using UniVerein.Api.Security;
using UniVerein.Api.Models.Enums;
using UniVerein.Api.Services;
using Microsoft.AspNetCore.Mvc;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace UniVerein.Api.Controllers;

[ApiController]
[Route("users")]
[EnableCors("AllowFrontend")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly CryptoService _cryptoService;
    private readonly TimeProvider _timeProvider;
    private readonly AuditService _auditService;

    public UsersController(AppDbContext db, CryptoService cryptoService, TimeProvider timeProvider, AuditService auditService)
    {
        _db = db;
        _cryptoService = cryptoService;
        _timeProvider = timeProvider;
        _auditService = auditService;
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpGet]
    public async Task<ActionResult<List<UserResult>>> GetAllAsync()
    {
        List<UserEntity> users = await _db.Users.ToListAsync();
        List<UserResult> result = users.Select(u => new UserResult()
        {
            Id = u.Id,
            Username = u.Username,
            Email = _cryptoService.Decrypt(u.Email) ?? string.Empty,
            Role = u.Role
        }).ToList();

        return Ok(new UserResults()
        {
            Items = result,
            Total = result.Count
        });
    }

    [Authorize]
    [HttpGet("account")]
    public async Task<ActionResult<UserResult>> GetAccountUserAsync()
    {
        string? userIdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out Guid id))
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "User not valid."));

        UserEntity? userEntity = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (userEntity == null)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "User not found.",
                moreInfo: $"User not found. User with ID: {id} not found."));

        return Ok(new UserResult()
        {
            Id = userEntity.Id,
            Username = userEntity.Username,
            Email = _cryptoService.Decrypt(userEntity.Email) ?? string.Empty,
            Role = userEntity.Role
        });
    }

    [Authorize]
    [HttpPatch("account")]
    public async Task<ActionResult<UserResult>> UpdateAccountUserAsync([FromBody] UserUpdateRequest request)
    {
        string? userIdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out Guid id))
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "User not valid."));

        request.Role = null;
        return await UpdateAsync(id, request);
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpPost]
    public async Task<ActionResult<UserResult>> CreateAsync([FromBody] UserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length > 50)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: "Username length must be greater than 0 or less 51 characters."));
        if (request.Password.Length < 10 || request.Password.Length > 50)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: "Password length must be greater than 9 or less 51 characters."));
        if (!string.IsNullOrWhiteSpace(request.Email) && request.Email.Length > 50)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: "Email length must be less 50 characters."));
        if (await _db.Users.AnyAsync(u => u.Username == request.Username))
            return Conflict(new ApiResults.ErrorResults.ConflictResult(errorCode: ApiErrorCodes.CONFLICT_RESOURCE_ALREADY_EXISTS,
                errorMessage: "Username already exists.",
                moreInfo: "User with same name already exists. Try a other name."));

        UserEntity user = new()
        {
            Username = request.Username,
            Email = _cryptoService.Encrypt(request.Email),
            Role = request.Role,
            PasswordHash = CryptoService.HashPassword(request.Password)
        };

        await _db.Users.AddAsync(user);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(AuditLogActions.CREATE, nameof(UserEntity),
            new { UserId = user.Id, user.Username, user.Role });
        Log.Information("UsersController: CreateAsync -> User {UserId} created with role {Role} by {ActorId}.",
            user.Id, user.Role, HttpContext.GetActorId());

        UserResult result = new()
        {
            Id = user.Id,
            Username = user.Username,
            Email = _cryptoService.Decrypt(user.Email) ?? string.Empty,
            Role = user.Role
        };

        return Created("", result);
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpPatch("{id}")]
    public async Task<ActionResult<UserResult>> UpdateAsync([FromRoute] Guid id, [FromBody] UserUpdateRequest request)
    {
        UserEntity? user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "User not found.",
                moreInfo: $"User with ID {id} not found."));

        List<string> changedFields = new();
        UserRole oldRole = user.Role;

        if (!string.IsNullOrWhiteSpace(request.Password) && IsCurrentUser(id))
        {
            ActionResult? verifyError = await VerifyCurrentPasswordAsync(user, request.CurrentPassword);
            if (verifyError != null)
                return verifyError;
        }

        if (!string.IsNullOrWhiteSpace(request.Username))
        {
            if (request.Username.Length > 50)
                return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                    moreInfo: "Username length must be greater than 0 or less 51 characters."));

            if (await _db.Users.AnyAsync(u => u.Username == request.Username && u.Id != id))
                return Conflict(new ApiResults.ErrorResults.ConflictResult(errorCode: ApiErrorCodes.CONFLICT_RESOURCE_ALREADY_EXISTS,
                    errorMessage: "Username already exists.",
                    moreInfo: "User with same name already exists. Try a other name."));

            user.Username = request.Username;
            changedFields.Add("username");
        }

        if (request.Email != null)
        {
            if (request.Email.Length > 50)
                return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Email length must be less 51 characters."));

            user.Email = _cryptoService.Encrypt(request.Email);
            changedFields.Add("email");
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            if (request.Password.Length < 10 || request.Password.Length > 50)
                return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                    moreInfo: "Password length must be greater than 9 or less 51 characters."));

            user.PasswordHash = CryptoService.HashPassword(request.Password);
            changedFields.Add("password");
        }

        if (request.Role != null && request.Role != user.Role)
        {
            if (user.Role == UserRole.ADMIN && IsCurrentUser(id))
                return StatusCode(StatusCodes.Status403Forbidden, new ApiResults.ErrorResults.ForbiddenRequestResult(
                    errorMessage: "Admins cannot change their own role.",
                    moreInfo: "Role changes for your own account are not allowed."));

            user.Role = (UserRole)request.Role!;
            changedFields.Add("role");
        }

        _db.Users.Update(user);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(AuditLogActions.UPDATE, nameof(UserEntity),
            new { UserId = user.Id, user.Username, ChangedFields = changedFields, OldRole = oldRole, NewRole = user.Role });
        Log.Information("UsersController: UpdateAsync -> User {UserId} updated by {ActorId}, changed fields: {ChangedFields}, role: {OldRole} -> {NewRole}.",
            user.Id, HttpContext.GetActorId(), string.Join(",", changedFields), oldRole, user.Role);
        return Ok(new UserResult()
        {
            Id = user.Id,
            Username = user.Username,
            Email = _cryptoService.Decrypt(user.Email) ?? string.Empty,
            Role = user.Role
        });
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] Guid id)
    {
        UserEntity? user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "User not found.",
                moreInfo: $"User with ID {id} not found."));

        if (user.Role == UserRole.ADMIN && IsCurrentUser(id))
            return StatusCode(StatusCodes.Status403Forbidden, new ApiResults.ErrorResults.ForbiddenRequestResult(
                errorMessage: "Admins cannot delete their own account.",
                moreInfo: "Self-deletion is not allowed for admin accounts."));

        _db.Remove(user);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(AuditLogActions.SOFT_DELETE, nameof(UserEntity),
            new { UserId = id, user.Username, user.Role });
        Log.Information("UsersController: DeleteAsync -> User {UserId} deleted by {ActorId}.", id, HttpContext.GetActorId());
        return Ok();
    }

    private async Task<ActionResult?> VerifyCurrentPasswordAsync(UserEntity user, string? currentPassword)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();

        if (user.BlockingLoginTimeout.HasValue && user.BlockingLoginTimeout > now)
        {
            Log.Warning("UsersController: VerifyCurrentPasswordAsync -> Password change for user {UserId} blocked by lockout.", user.Id);
            return StatusCode(StatusCodes.Status403Forbidden, new ApiResults.ErrorResults.ForbiddenRequestResult(
                errorMessage: "To many failed password attempts.",
                moreInfo: $"Try again in {Math.Ceiling((user.BlockingLoginTimeout.Value - now).TotalSeconds)} seconds."));
        }

        bool verified;
        try
        {
            verified = CryptoService.VerifyPassword(currentPassword ?? string.Empty, user.PasswordHash);
        }
        catch (Exception ex)
        {
            Log.Warning("UsersController: Current password validation for user {UserId} threw {ExceptionType}.", user.Id, ex.GetType().Name);
            verified = false;
        }

        if (!verified)
        {
            user.FailedAttempts = LoginGuard.GetEffectiveFailedAttempts(user.FailedAttempts, user.LastFailedLoginAttempt, now) + 1;
            user.LastFailedLoginAttempt = now;
            user.BlockingLoginTimeout = LoginGuard.GetLockoutReleaseTime(user.FailedAttempts, now);
            await _db.SaveChangesAsync();
            Log.Warning("UsersController: VerifyCurrentPasswordAsync -> Wrong current password for user {UserId}, failed attempts: {FailedAttempts}, locked: {Locked}.",
                user.Id, user.FailedAttempts, user.BlockingLoginTimeout.HasValue);
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Current password is incorrect."));
        }

        user.FailedAttempts = 0;
        user.LastFailedLoginAttempt = null;
        user.BlockingLoginTimeout = null;
        return null;
    }

    private bool IsCurrentUser(Guid userId)
    {
        string? currentUserIdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(currentUserIdClaim, out Guid currentUserId) && currentUserId == userId;
    }
}