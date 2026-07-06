using System;
using System.Threading.Tasks;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Services;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace UniVerein.Api.Controllers;

[Authorize(Roles = nameof(UserRole.ADMIN))]
[ApiController]
[Route("creditor-config")]
public class CreditorConfigController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly CryptoService _cryptoService;

    public CreditorConfigController(AppDbContext db, CryptoService cryptoService)
    {
        _db = db;
        _cryptoService = cryptoService;
    }

    [HttpGet]
    public async Task<ActionResult<CreditorConfigResult>> GetAsync()
    {
        CreditorConfigEntity? creditorConfig = await _db.CreditorConfigs.FirstOrDefaultAsync(x => x.DeletedAt == null);
        if (creditorConfig == null)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Creditor config not found.",
                moreInfo: $"Creditor config not found. Creditor config not configured."));

        CreditorConfigResult result = new()
        {
            Id = creditorConfig.Id,
            Name = creditorConfig.Name,
            Iban = _cryptoService.Decrypt(creditorConfig.Iban_Encrypted) ?? string.Empty,
            Bic = _cryptoService.Decrypt(creditorConfig.Bic_Encrypted) ?? string.Empty,
            CreditorId = creditorConfig.CreditorId,
            StreetNameAndNumber = creditorConfig.StreetNameAndNumber,
            PostCode = creditorConfig.PostCode,
            CityName = creditorConfig.CityName,
            CountryCode = creditorConfig.CountryCode
        };

        return Ok(result);
    }

    [HttpPut]
    public async Task<ActionResult<CreditorConfigResult>> UpdateAsync([FromBody] CreditorConfigRequest request)
    {
        CreditorConfigEntity? creditorConfig = await _db.CreditorConfigs.FirstOrDefaultAsync();
        if (creditorConfig == null)
        {
            creditorConfig = new()
            {
                Name = request.Name,
                Iban_Encrypted = _cryptoService.Encrypt(request.Iban),
                Bic_Encrypted = _cryptoService.Encrypt(request.Bic),
                CreditorId = request.CreditorId,
                StreetNameAndNumber = request.StreetNameAndNumber,
                PostCode = request.PostCode,
                CityName = request.CityName,
                CountryCode = request.CountryCode
            };
            await _db.CreditorConfigs.AddAsync(creditorConfig);
        }
        else
        {
            creditorConfig.Name = request.Name;
            creditorConfig.Iban_Encrypted = _cryptoService.Encrypt(request.Iban);
            creditorConfig.Bic_Encrypted = _cryptoService.Encrypt(request.Bic);
            creditorConfig.CreditorId = request.CreditorId;
            creditorConfig.StreetNameAndNumber = request.StreetNameAndNumber;
            creditorConfig.PostCode = request.PostCode;
            creditorConfig.CityName = request.CityName;
            creditorConfig.CountryCode = request.CountryCode;
            creditorConfig.DeletedAt = null;
            _db.CreditorConfigs.Update(creditorConfig);
        }

        await _db.SaveChangesAsync();

        CreditorConfigResult result = new()
        {
            Id = creditorConfig.Id,
            Name = creditorConfig.Name,
            Iban = _cryptoService.Decrypt(creditorConfig.Iban_Encrypted) ?? string.Empty,
            Bic = _cryptoService.Decrypt(creditorConfig.Bic_Encrypted) ?? string.Empty,
            CreditorId = creditorConfig.CreditorId,
            StreetNameAndNumber = creditorConfig.StreetNameAndNumber,
            PostCode = creditorConfig.PostCode,
            CityName = creditorConfig.CityName,
            CountryCode = creditorConfig.CountryCode
        };
        return Ok(result);
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] Guid id)
    {
        CreditorConfigEntity? creditorConfig = await _db.CreditorConfigs.FindAsync(id);
        if (creditorConfig == null)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Creditor config not found.",
                moreInfo: $"Creditor config not found. Creditor config not configured."));

        _db.Remove(creditorConfig);
        await _db.SaveChangesAsync();
        return Ok();
    }
}