using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using UniVerein.Api.ApiResults.Finance;
using UniVerein.Api.Models.Eur;
using UniVerein.Api.Services;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Controllers;

[Authorize(Roles = $"{nameof(UserRole.ADMIN)},{nameof(UserRole.FINANCIAL_MANAGER)}")]
[ApiController]
[Route("finance")]
[EnableCors("AllowFrontend")]
public class FinanceController : ControllerBase
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const int MinEurYear = 2020;

    private readonly FinanceAnalyticsService _financeAnalyticsService;
    private readonly EurExportService _eurExportService;
    private readonly EurWorkbookWriter _eurWorkbookWriter;
    private readonly TimeProvider _timeProvider;

    public FinanceController(FinanceAnalyticsService financeAnalyticsService, EurExportService eurExportService,
        EurWorkbookWriter eurWorkbookWriter, TimeProvider timeProvider)
    {
        _financeAnalyticsService = financeAnalyticsService;
        _eurExportService = eurExportService;
        _eurWorkbookWriter = eurWorkbookWriter;
        _timeProvider = timeProvider;
    }

    [HttpGet("analytics")]
    public async Task<ActionResult<FinanceAnalyticsResult>> GetAnalyticsAsync()
    {
        return Ok(await _financeAnalyticsService.GetAnalyticsAsync());
    }

    [HttpGet("eur")]
    public async Task<IActionResult> GetEurAsync([FromQuery] int year)
    {
        int maxYear = _timeProvider.GetUtcNow().Year + 1;
        if (year < MinEurYear || year > maxYear)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: $"Year must be between {MinEurYear} and {maxYear}."));

        EurReport report = await _eurExportService.BuildAsync(year);
        return File(_eurWorkbookWriter.Write(report), XlsxContentType, $"EUER_{year}.xlsx");
    }
}
