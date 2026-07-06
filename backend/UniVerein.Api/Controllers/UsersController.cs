using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Services;
using Microsoft.AspNetCore.Mvc;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.EntityFrameworkCore;

namespace UniVerein.Api.Controllers;

[ApiController]
[Route("users")]
[EnableCors("AllowFrontend")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly CryptoService _cryptoService;

    public UsersController(AppDbContext db, CryptoService cryptoService)
    {
        _db = db;
        _cryptoService = cryptoService;
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
        var userIdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
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
        var userIdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
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
        }

        if (request.Email != null)
        {
            if (request.Email.Length > 50)
                return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Email length must be less 51 characters."));

            user.Email = _cryptoService.Encrypt(request.Email);
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            if (request.Password.Length < 10 || request.Password.Length > 50)
                return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                    moreInfo: "Password length must be greater than 9 or less 51 characters."));

            user.PasswordHash = CryptoService.HashPassword(request.Password);
        }

        if (request.Role != null)
            user.Role = (UserRole)request.Role!;

        _db.Users.Update(user);
        await _db.SaveChangesAsync();
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

        _db.Remove(user);
        await _db.SaveChangesAsync();
        return Ok();
    }
}