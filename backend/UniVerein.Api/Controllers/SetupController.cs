using System;
using System.Data;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Services;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace UniVerein.Api.Controllers;

[ApiController]
[Route("setup")]
[EnableCors("AllowFrontend")]
public class SetupController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly CryptoService _cryptoService;

    public SetupController(AppDbContext db, IConfiguration configuration, CryptoService cryptoService)
    {
        _db = db;
        _configuration = configuration;
        _cryptoService = cryptoService;
    }

    [AllowAnonymous]
    [EnableRateLimiting("setup-status")]
    [HttpGet("status")]
    public async Task<ActionResult<SetupStatusResult>> GetStatusAsync()
    {
        return Ok(new SetupStatusResult() { NeedsSetup = await NeedsSetupAsync() });
    }

    [AllowAnonymous]
    [EnableRateLimiting("setup")]
    [HttpPost]
    public async Task<ActionResult> SetupAsync([FromBody] SetupRequest request)
    {
        if (!await NeedsSetupAsync())
            return AlreadySetUp();

        string? configuredToken = _configuration["Setup:Token"];
        if (string.IsNullOrWhiteSpace(configuredToken))
        {
            Log.Warning("SetupController: SetupAsync -> Setup is locked because no setup token is configured.");
            return StatusCode(StatusCodes.Status403Forbidden, new ApiResults.ErrorResults.ForbiddenRequestResult(
                moreInfo: "Setup token invalid."));
        }

        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(request.Token),
                Encoding.UTF8.GetBytes(configuredToken)))
        {
            Log.Warning("SetupController: SetupAsync -> Setup token mismatch.");
            return StatusCode(StatusCodes.Status403Forbidden, new ApiResults.ErrorResults.ForbiddenRequestResult(
                moreInfo: "Setup token invalid."));
        }

        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length > 50)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: "Username length must be greater than 0 or less 51 characters."));
        if (request.Password.Length < 10 || request.Password.Length > 50)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: "Password length must be greater than 9 or less 51 characters."));
        if (request.Email.Length > 50 || !MailAddress.TryCreate(request.Email, out _))
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: "Email must be a valid address with less than 51 characters."));

        UserEntity user = new()
        {
            Username = request.Username,
            Email = _cryptoService.Encrypt(request.Email),
            Role = UserRole.ADMIN,
            PasswordHash = CryptoService.HashPassword(request.Password)
        };

        try
        {
            await using IDbContextTransaction transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            if (!await NeedsSetupAsync())
                return AlreadySetUp();

            await _db.Users.AddAsync(user);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            return AlreadySetUp();
        }

        Log.Information("SetupController: SetupAsync -> Initial admin user {UserId} created.", user.Id);
        return NoContent();
    }

    private async Task<bool> NeedsSetupAsync()
    {
        return !await _db.Users.IgnoreQueryFilters(QueryFilter.SOFT_DELETE).AnyAsync();
    }

    private ActionResult AlreadySetUp()
    {
        return Conflict(new ApiResults.ErrorResults.ConflictResult(errorCode: ApiErrorCodes.CONFLICT_RESOURCE_ALREADY_EXISTS,
            errorMessage: "Setup already completed.",
            moreInfo: "The system already has users. Log in instead."));
    }
}
