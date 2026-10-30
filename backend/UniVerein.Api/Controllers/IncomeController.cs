using System;
using System.Security.Claims;
using System.Threading.Tasks;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults.Income;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Extensions;
using UniVerein.Api.Helper;
using UniVerein.Api.Models.Enums;
using UniVerein.Api.Query;
using UniVerein.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using Serilog;

namespace UniVerein.Api.Controllers;

[Authorize]
[ApiController]
[Route("income")]
[EnableCors("AllowFrontend")]
public class IncomeController : ControllerBase
{
    private const string ManagerRoles = $"{nameof(UserRole.ADMIN)},{nameof(UserRole.FINANCIAL_MANAGER)}";

    private readonly AppDbContext _db;
    private readonly IncomeService _incomeService;
    private readonly AuditService _auditService;
    private readonly TimeProvider _timeProvider;
    private readonly BookingNumberService _bookingNumberService;

    public IncomeController(AppDbContext db, IncomeService incomeService, AuditService auditService,
        TimeProvider timeProvider, BookingNumberService bookingNumberService)
    {
        _db = db;
        _incomeService = incomeService;
        _auditService = auditService;
        _timeProvider = timeProvider;
        _bookingNumberService = bookingNumberService;
    }

    [HttpGet]
    public async Task<ActionResult<IncomeRowsResult>> GetAsync([FromQuery] IncomeQuery incomeQuery)
    {
        if (incomeQuery.Offset < 0 || incomeQuery.Limit < 1)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Offset and/or Limit must be greater than or equal to 1."));

        bool showDeleted = User.FindFirst(ClaimTypes.Role)?.Value == nameof(UserRole.ADMIN)
                           && incomeQuery.Deleted == true;

        return Ok(await _incomeService.GetRowsAsync(incomeQuery, showDeleted));
    }

    [HttpGet("years")]
    public async Task<ActionResult<IncomeYearsResult>> GetYearsAsync()
    {
        return Ok(await _incomeService.GetYearsAsync());
    }

    [HttpGet("summary")]
    public async Task<ActionResult<IncomeSummaryResult>> GetSummaryAsync([FromQuery] IncomeSummaryQuery summaryQuery)
    {
        return Ok(await _incomeService.GetSummaryAsync(summaryQuery));
    }

    [Authorize(Roles = ManagerRoles)]
    [HttpPost]
    public async Task<ActionResult<IncomeRowResult>> CreateAsync([FromBody] IncomeRequest request)
    {
        (IncomeCategoryEntity? category, string? error) = await ValidateAsync(request.CategoryId, request.Payer, request.Amount, request.IncomeDate);
        if (error != null)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: error));

        IncomeEntity income = new()
        {
            Id = Guid.NewGuid(),
            CategoryId = request.CategoryId,
            Category = category,
            Payer = request.Payer.Trim(),
            Description = NormalizeDescription(request.Description),
            Amount = request.Amount,
            IncomeDate = IncomeService.ToUtcDate(request.IncomeDate),
            Paid = request.Paid ? _timeProvider.GetUtcNow() : null,
            PaymentDate = request.Paid ? IncomeService.ToUtcDate(request.PaymentDate ?? request.IncomeDate) : null
        };
        await _db.Incomes.AddAsync(income);

        BookingNumberEntity? number = null;
        if (request.Paid)
            number = await _bookingNumberService.TryAssignIncomeAsync(income, income.PaymentDate!.Value);
        else
            await _db.SaveChangesAsync();

        await _auditService.LogAsync(AuditLogActions.CREATE, nameof(IncomeEntity),
            new { IncomeId = income.Id, income.CategoryId, income.Amount, Paid = request.Paid, income.PaymentDate, BookingNumber = number?.Number });
        Log.Information("IncomeController: CreateAsync -> Income {IncomeId} created by {ActorId}.", income.Id, HttpContext.GetActorId());

        IncomeRowResult row = IncomeService.ToRow(income);
        row.BookingNumber = number?.Number;
        return Created("", row);
    }

    [Authorize(Roles = ManagerRoles)]
    [HttpPut("{id}")]
    public async Task<ActionResult<IncomeRowResult>> UpdateAsync([FromRoute] Guid id, [FromBody] IncomeUpdateRequest request)
    {
        IncomeEntity? income = await _db.Incomes.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null);
        if (income == null)
            return NotFoundResult(id);

        if (await _db.PaymentAcknowledgements.AnyAsync(x => x.IncomeId == id))
            return AcknowledgementConflict();

        (IncomeCategoryEntity? category, string? error) = await ValidateAsync(request.CategoryId, request.Payer, request.Amount, request.IncomeDate);
        if (error != null)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: error));

        income.CategoryId = request.CategoryId;
        income.Category = category;
        income.Payer = request.Payer.Trim();
        income.Description = NormalizeDescription(request.Description);
        income.Amount = request.Amount;
        income.IncomeDate = IncomeService.ToUtcDate(request.IncomeDate);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(AuditLogActions.UPDATE, nameof(IncomeEntity),
            new { IncomeId = id, income.CategoryId, income.Amount });
        Log.Information("IncomeController: UpdateAsync -> Income {IncomeId} updated by {ActorId}.", id, HttpContext.GetActorId());

        return Ok(IncomeService.ToRow(income));
    }

    [Authorize(Roles = ManagerRoles)]
    [HttpPost("{id}")]
    public async Task<IActionResult> MarkAsPaidAsync([FromRoute] Guid id, [FromBody] MarkPaidRequest request)
    {
        IncomeEntity? income = await _db.Incomes.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null);
        if (income == null)
            return NotFoundResult(id);

        if (await _db.PaymentAcknowledgements.AnyAsync(x => x.IncomeId == id))
            return AcknowledgementConflict();

        string? bookingNumber = null;
        if (request.Paid)
        {
            DateTime effectivePaymentDate = IncomeService.ToUtcDate(request.PaymentDate ?? _timeProvider.GetUtcNow().UtcDateTime);
            income.Paid = _timeProvider.GetUtcNow();
            income.PaymentDate = effectivePaymentDate;

            BookingNumberEntity? number = await _bookingNumberService.TryAssignIncomeAsync(income, effectivePaymentDate);
            if (number == null)
                return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                    moreInfo: "The payment date must be in the year of the booking number that was already assigned."));

            bookingNumber = number.Number;
        }
        else
        {
            income.Paid = null;
            income.PaymentDate = null;
            await _db.SaveChangesAsync();
        }

        await _auditService.LogAsync(AuditLogActions.UPDATE, nameof(IncomeEntity),
            new { IncomeId = id, income.Amount, Paid = request.Paid, income.PaymentDate, BookingNumber = bookingNumber });
        Log.Information("IncomeController: MarkAsPaidAsync -> Income {IncomeId} marked as {State} by {ActorId}.",
            id, request.Paid ? "paid" : "unpaid", HttpContext.GetActorId());

        return Ok();
    }

    [Authorize(Roles = ManagerRoles)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] Guid id)
    {
        IncomeEntity? income = await _db.Incomes.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null);
        if (income == null)
            return NotFoundResult(id);

        if (income.Paid != null)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Only open incomes can be deleted."));

        _db.Incomes.Remove(income);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(AuditLogActions.SOFT_DELETE, nameof(IncomeEntity),
            new { IncomeId = id, income.Amount });
        Log.Information("IncomeController: DeleteAsync -> Income {IncomeId} soft-deleted by {ActorId}.", id, HttpContext.GetActorId());

        return Ok();
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpPost("{id}/restore")]
    public async Task<IActionResult> RestoreAsync([FromRoute] Guid id)
    {
        IncomeEntity? income = await _db.Incomes.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt != null);
        if (income == null)
            return NotFoundResult(id);

        income.DeletedAt = null;
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(AuditLogActions.RESTORE, nameof(IncomeEntity),
            new { IncomeId = id, income.Amount });
        Log.Information("IncomeController: RestoreAsync -> Income {IncomeId} restored by {ActorId}.", id, HttpContext.GetActorId());

        return Ok();
    }

    private async Task<(IncomeCategoryEntity? Category, string? Error)> ValidateAsync(Guid categoryId, string payer,
        decimal amount, DateTime incomeDate)
    {
        if (amount <= 0)
            return (null, "Amount must be greater than 0.");

        if (amount > MoneyLimits.MaxAmount)
            return (null, $"Amount must not exceed {MoneyLimits.MaxAmount:0}.");

        if (string.IsNullOrWhiteSpace(payer))
            return (null, "Payer is required.");

        if (incomeDate == default)
            return (null, "IncomeDate is required.");

        IncomeCategoryEntity? category = await _db.IncomeCategories.FirstOrDefaultAsync(c => c.Id == categoryId);
        if (category == null)
            return (null, "A valid category is required.");

        return (category, null);
    }

    private static string? NormalizeDescription(string? description)
    {
        return string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    private ConflictObjectResult AcknowledgementConflict()
    {
        return Conflict(new ApiResults.ErrorResults.ConflictResult(errorCode: ApiErrorCodes.CONFLICT_RESOURCE_MODIFIED,
            errorMessage: "Payment acknowledgement already issued.",
            moreInfo: "An issued payment acknowledgement exists for this income, so it can no longer be changed or reopened."));
    }

    private NotFoundObjectResult NotFoundResult(Guid id)
    {
        return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
            errorMessage: "Income not found.",
            moreInfo: $"Income with ID {id} not found."));
    }
}
