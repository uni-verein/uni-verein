using System;
using System.Linq;
using System.Threading.Tasks;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Models;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Query;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniVerein.Api.Services;
using UniVerein.Api.Services.Sepa;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using Microsoft.AspNetCore.Cors;
using Microsoft.EntityFrameworkCore;

namespace UniVerein.Api.Controllers;

[ApiController]
[Route("sepa")]
[EnableCors("AllowFrontend")]
public class SepaController : ControllerBase
{
    private readonly SepaService _sepa;
    private readonly AppDbContext _db;
    private readonly CryptoService _cryptoService;

    public SepaController(SepaService sepa, AppDbContext db, CryptoService cryptoService)
    {
        _sepa = sepa;
        _db = db;
        _cryptoService = cryptoService;
    }

    [Authorize(Roles = $"{nameof(UserRole.ADMIN)},{nameof(UserRole.FINANCIAL_MANAGER)}")]
    [HttpGet("export/{id}")]
    public async Task<IActionResult> Export(Guid id)
    {
        CreditorConfigEntity? creditorConfig = await _db.CreditorConfigs.FirstOrDefaultAsync(x => x.DeletedAt == null);
        if (creditorConfig == null)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Bank details not found",
                moreInfo: "No bank details could be found. Please check whether bank details have been configured."));

        try
        {
            (string xml, decimal _, int _) = await _sepa.GenerateXml(new CreditorConfig()
            {
                Name = creditorConfig.Name,
                Iban = _cryptoService.Decrypt(creditorConfig.Iban_Encrypted) ?? string.Empty,
                Bic = _cryptoService.Decrypt(creditorConfig.Bic_Encrypted) ?? string.Empty,
                CreditorId = creditorConfig.CreditorId,
                TownName = creditorConfig.CityName,
                Country = creditorConfig.CountryCode
            }, id);

            return File(System.Text.Encoding.UTF8.GetBytes(xml), "application/xml", "sepa.xml");
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(207, new ErrorDetailsResult()
            {
                ErrorMessage = ex.Message,
                MoreInfo = ex.InnerException?.Message
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: ex.Message));
        }
    }

    [Authorize(Roles = $"{nameof(UserRole.ADMIN)},{nameof(UserRole.FINANCIAL_MANAGER)}")]
    [HttpGet("exports")]
    public async Task<ActionResult<AllSepaExportInfoResults>> Exports(
        [FromQuery] ContributionInfoQuery contributionInfoQuery)
    {
        IQueryable<SepaExportInfoResult> query = _db.SepaExports
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new SepaExportInfoResult()
            {
                Id = x.Id,
                Name = x.Name,
                Amount = x.Amount,
                ExportedCases = x.Count,
                ExportedDate = x.CreatedAt
            });

        int count = await query.CountAsync();
        if (count == 0)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "No SEPA exports found.",
                moreInfo: "No SEPA exports were found."));

        AllSepaExportInfoResults result = new()
        {
            Items = await query.Skip(contributionInfoQuery.Offset).Take(contributionInfoQuery.Limit).ToListAsync(),
            Total = count
        };

        return Ok(result);
    }
}