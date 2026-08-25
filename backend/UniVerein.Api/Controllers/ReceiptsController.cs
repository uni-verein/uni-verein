using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Models;
using UniVerein.Api.Models.Enums;
using UniVerein.Api.Query;
using UniVerein.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using Serilog;
using UniVerein.Api.ApiResults.Receipt;

namespace UniVerein.Api.Controllers;

[Authorize]
[ApiController]
[Route("receipts")]
[EnableCors("AllowFrontend")]
public class ReceiptsController : ControllerBase
{
    // How long after creation a receipt may still be corrected (e.g. fixing a typo) instead of
    // having to be deleted and re-submitted from scratch.
    private static readonly TimeSpan EditWindow = TimeSpan.FromMinutes(15);

    private readonly AppDbContext _db;
    private readonly ReceiptService _receiptService;
    private readonly ReceiptNotificationService _receiptNotificationService;
    private readonly AuditService _auditService;
    private readonly TimeProvider _timeProvider;
    private readonly Guid _currentUserId;
    private readonly bool _isAdmin;
    private readonly bool _isPrivileged;

    public ReceiptsController(AppDbContext db, ReceiptService receiptService,
        ReceiptNotificationService receiptNotificationService, AuditService auditService,
        TimeProvider timeProvider, IHttpContextAccessor http)
    {
        _db = db;
        _receiptService = receiptService;
        _receiptNotificationService = receiptNotificationService;
        _auditService = auditService;
        _timeProvider = timeProvider;

        string? userIdClaim = http.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        _currentUserId = Guid.TryParse(userIdClaim, out Guid id) ? id : Guid.Empty;

        string? role = http.HttpContext?.User.FindFirst(ClaimTypes.Role)?.Value;
        _isAdmin = role == nameof(UserRole.ADMIN);
        _isPrivileged = role is nameof(UserRole.ADMIN) or nameof(UserRole.FINANCIAL_MANAGER);
    }

    [Authorize(Roles = $"{nameof(UserRole.ADMIN)},{nameof(UserRole.FINANCIAL_MANAGER)}")]
    [HttpGet("analytics")]
    public async Task<ActionResult<ReceiptAnalyticsResult>> GetAnalyticsAsync([FromQuery] int? year)
    {
        int targetYear = year ?? DateTime.UtcNow.Year;

        List<ReceiptAnalyticsRow> receipts = await _db.Receipts
            .Where(r => r.DeletedAt == null)
            .GroupBy(r => new
            {
                Year = r.ReceiptDate.Year,
                Month = r.ReceiptDate.Month,
                r.CategoryId,
                CategoryName = r.Category != null ? r.Category.Name : null
            })
            .Select(g => new ReceiptAnalyticsRow
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                CategoryId = g.Key.CategoryId,
                CategoryName = g.Key.CategoryName,
                Amount = g.Sum(r => r.Amount)
            })
            .ToListAsync();

        List<ReceiptCategoryTotal> ByCategoryTotals(IEnumerable<ReceiptAnalyticsRow> rows) => rows
            .GroupBy(r => r.CategoryId)
            .Select(g => new ReceiptCategoryTotal
            {
                CategoryId = g.Key,
                CategoryName = g.First().CategoryName,
                Total = g.Sum(r => r.Amount)
            })
            .OrderByDescending(x => x.Total)
            .ToList();

        List<ReceiptYearlyTotal> byYear = receipts
            .GroupBy(r => r.Year)
            .Select(g => new ReceiptYearlyTotal
            {
                Year = g.Key,
                Total = g.Sum(r => r.Amount),
                ByCategory = ByCategoryTotals(g)
            })
            .OrderBy(x => x.Year)
            .ToList();

        List<ReceiptAnalyticsRow> receiptsForYear = receipts.Where(r => r.Year == targetYear).ToList();

        List<ReceiptMonthlyTotal> byMonth = receiptsForYear
            .GroupBy(r => r.Month)
            .Select(g => new ReceiptMonthlyTotal
            {
                Month = g.Key,
                Total = g.Sum(r => r.Amount),
                ByCategory = ByCategoryTotals(g)
            })
            .OrderBy(x => x.Month)
            .ToList();

        List<ReceiptCategoryTotal> byCategory = ByCategoryTotals(receiptsForYear);

        return Ok(new ReceiptAnalyticsResult
        {
            Year = targetYear,
            ByYear = byYear,
            ByMonth = byMonth,
            ByCategory = byCategory
        });
    }

    [Authorize(Roles = $"{nameof(UserRole.ADMIN)},{nameof(UserRole.FINANCIAL_MANAGER)}")]
    [HttpGet("export")]
    public async Task<IActionResult> ExportAsync([FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo, [FromQuery] Guid? categoryId)
    {
        IQueryable<ReceiptEntity> query = _db.Receipts
            .Include(r => r.User)
            .Include(r => r.Category)
            .Include(r => r.Files)
            .Where(r => r.DeletedAt == null);

        if (dateFrom != null)
            query = query.Where(r => r.ReceiptDate >= dateFrom);

        if (dateTo != null)
            query = query.Where(r => r.ReceiptDate <= dateTo);

        if (categoryId != null)
            query = query.Where(r => r.CategoryId == categoryId);

        List<ReceiptEntity> receipts = await query.OrderBy(r => r.ReceiptDate).ToListAsync();

        List<ReceiptExportRow> rows = receipts.Select(r => new ReceiptExportRow
        {
            ReceiptDate = r.ReceiptDate,
            Amount = r.Amount,
            Category = r.Category?.Name ?? string.Empty,
            Vendor = r.Vendor ?? string.Empty,
            Description = r.Description ?? string.Empty,
            PaymentMethod = r.PaymentMethod?.ToString() ?? string.Empty,
            SubmittedBy = r.User?.Username ?? "(deleted user)",
            FileIds = string.Join(",", r.Files.Select(f => f.Id))
        }).ToList();

        CsvConfiguration config = new(CultureInfo.InvariantCulture)
        {
            Delimiter = ";",
            HasHeaderRecord = true,
            Encoding = Encoding.UTF8
        };

        using MemoryStream memoryStream = new();
        await using StreamWriter streamWriter =
            new(memoryStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        await using CsvWriter csvWriter = new(streamWriter, config);
        await csvWriter.WriteRecordsAsync(rows);
        await streamWriter.FlushAsync();

        List<ReceiptFileEntity> allFiles = receipts.SelectMany(r => r.Files).ToList();

        Log.Information(
            $"ReceiptsController: ExportAsync -> {rows.Count} receipts, {allFiles.Count} files exported.");

        IHttpBodyControlFeature? bodyControlFeature = HttpContext.Features.Get<IHttpBodyControlFeature>();
        if (bodyControlFeature != null)
            bodyControlFeature.AllowSynchronousIO = true;

        Response.ContentType = "application/zip";
        Response.Headers.ContentDisposition =
            $"attachment; filename=\"receipts_export_{DateTime.UtcNow:ddMMyyyy_HHmmss}.zip\"";
        await _receiptService.WriteExportZipAsync(Response.Body, memoryStream.ToArray(), allFiles);

        return new EmptyResult();
    }

    [HttpGet]
    public async Task<ActionResult<AllReceiptResults>> GetAllAsync([FromQuery] ReceiptQuery query)
    {
        if (query.Offset < 0 || query.Limit < 1)
            return BadRequest(
                new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Offset and/or Limit must be greater than or equal to 1."));

        IQueryable<ReceiptEntity> receiptQuery = _db.Receipts
            .Include(r => r.User)
            .Include(r => r.Category)
            .Include(r => r.Files)
            .AsQueryable();

        if (!_isAdmin)
            receiptQuery = receiptQuery.Where(r => r.DeletedAt == null);
        else if (query.Deleted == true)
            receiptQuery = receiptQuery.Where(r => r.DeletedAt != null);

        if (!_isPrivileged)
            receiptQuery = receiptQuery.Where(r => r.UserId == _currentUserId);
        else if (query.UserId != null)
            receiptQuery = receiptQuery.Where(r => r.UserId == query.UserId);

        if (query.CategoryId != null)
            receiptQuery = receiptQuery.Where(r => r.CategoryId == query.CategoryId);

        if (query.DateFrom != null)
            receiptQuery = receiptQuery.Where(r => r.ReceiptDate >= query.DateFrom);

        if (query.DateTo != null)
            receiptQuery = receiptQuery.Where(r => r.ReceiptDate <= query.DateTo);

        if (query.Paid != null)
            receiptQuery = receiptQuery.Where(r => (r.Paid != null) == query.Paid.Value);

        receiptQuery = receiptQuery.OrderByDescending(r => r.ReceiptDate);

        int total = await receiptQuery.CountAsync();
        List<ReceiptEntity> items = await receiptQuery.Skip(query.Offset).Take(query.Limit).ToListAsync();

        return Ok(new AllReceiptResults()
        {
            Items = items.Select(MapToResult).ToList(),
            Total = total
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ReceiptResult>> GetAsync([FromRoute] Guid id)
    {
        ReceiptEntity? receipt = await _db.Receipts
            .Include(r => r.User)
            .Include(r => r.Category)
            .Include(r => r.Files)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (receipt == null)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Receipt not found.",
                moreInfo: $"No receipt with the ID {id} could be found."));

        if (!_isPrivileged && receipt.UserId != _currentUserId)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Receipt not found.",
                moreInfo: $"No receipt with the ID {id} could be found."));

        return Ok(MapToResult(receipt));
    }

    [HttpGet("{id}/files/{fileId}")]
    public async Task<ActionResult<byte[]>> GetFileAsync([FromRoute] Guid id, [FromRoute] Guid fileId)
    {
        ReceiptEntity? receipt = await _db.Receipts.FirstOrDefaultAsync(r => r.Id == id);
        if (receipt == null)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Receipt not found.",
                moreInfo: $"No receipt with the ID {id} could be found."));

        if (!_isPrivileged && receipt.UserId != _currentUserId)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Receipt not found.",
                moreInfo: $"No receipt with the ID {id} could be found."));

        ReceiptFileEntity? receiptFile = await _db.ReceiptFiles
            .FirstOrDefaultAsync(i => i.Id == fileId && i.ReceiptId == id);
        if (receiptFile == null)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Receipt file not found.",
                moreInfo: $"No file with the ID {fileId} could be found."));

        string path = _receiptService.GetFilePath(receiptFile);
        if (!System.IO.File.Exists(path))
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Receipt file not found on disk.",
                moreInfo: $"File for ID {fileId} could not be found on disk."));

        byte[] bytes = await System.IO.File.ReadAllBytesAsync(path);
        return File(bytes, receiptFile.ContentType);
    }

    [HttpPost]
    public async Task<ActionResult<ReceiptResult>> CreateAsync([FromForm] CreateReceiptRequest request)
    {
        if (request.Amount <= 0)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Amount must be greater than 0."));

        if (request.ReceiptDate == default)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "ReceiptDate is required."));

        if (request.CategoryId != null &&
            !await _db.ReceiptCategories.AnyAsync(c => c.Id == request.CategoryId))
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Category not found."));

        if (request.Files != null && request.Files.Any(f =>
                !f.ContentType.StartsWith("image/") && f.ContentType != "application/pdf"))
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Only image or PDF files are allowed."));

        if (!_isPrivileged && request.PaymentMethod != null)
            return StatusCode(StatusCodes.Status403Forbidden, new ApiResults.ErrorResults.ForbiddenRequestResult(
                errorMessage: "Only admins or financial managers may set the payment method.",
                moreInfo: "The payment method can only be selected by ADMIN or FINANCIAL_MANAGER."));

        UserEntity? user = await _db.Users.FindAsync(_currentUserId);
        if (user == null)
            return Unauthorized();

        ReceiptEntity receipt = new()
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            Amount = request.Amount,
            ReceiptDate = request.ReceiptDate,
            CategoryId = request.CategoryId,
            Vendor = request.Vendor,
            Description = request.Description,
            PaymentMethod = request.PaymentMethod
        };
        await _db.Receipts.AddAsync(receipt);

        if (request.Files != null)
        {
            int position = 0;
            foreach (IFormFile file in request.Files)
            {
                ReceiptFileEntity receiptFile = await _receiptService.SaveFileAsync(receipt, file, position++);
                await _db.ReceiptFiles.AddAsync(receiptFile);
            }
        }
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(AuditLogActions.CREATE, nameof(ReceiptEntity),
            new { ReceiptId = receipt.Id, receipt.Amount, receipt.ReceiptDate });

        Log.Information($"ReceiptsController: CreateAsync -> Receipt with ID: {receipt.Id} successfully created.");

        await _receiptNotificationService.NotifyFinancialManagersAsync(receipt, user);

        ReceiptEntity saved = await _db.Receipts
            .Include(r => r.User)
            .Include(r => r.Category)
            .Include(r => r.Files)
            .FirstAsync(r => r.Id == receipt.Id);

        return Created("", MapToResult(saved));
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult<ReceiptResult>> UpdateAsync([FromRoute] Guid id, [FromBody] UpdateReceiptRequest request)
    {
        ReceiptEntity? receipt = await _db.Receipts
            .Include(r => r.User)
            .Include(r => r.Category)
            .Include(r => r.Files)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (receipt == null || receipt.DeletedAt != null)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Receipt not found.",
                moreInfo: $"No receipt with the ID {id} could be found."));

        if (!_isPrivileged && receipt.UserId != _currentUserId)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Receipt not found.",
                moreInfo: $"No receipt with the ID {id} could be found."));

        if (receipt.UserId != _currentUserId)
            return StatusCode(StatusCodes.Status403Forbidden, new ApiResults.ErrorResults.ForbiddenRequestResult(
                errorMessage: "Only the creator of a receipt may edit it.",
                moreInfo: $"Receipt {id} can only be edited by the user who created it."));

        if (receipt.Paid != null)
            return StatusCode(StatusCodes.Status403Forbidden, new ApiResults.ErrorResults.ForbiddenRequestResult(
                errorMessage: "Receipt has already been marked as paid and can no longer be edited.",
                moreInfo: $"Receipt {id} is locked because it has been marked as paid."));

        TimeSpan age = _timeProvider.GetUtcNow() - receipt.CreatedAt;
        if (age > EditWindow)
            return StatusCode(StatusCodes.Status403Forbidden, new ApiResults.ErrorResults.ForbiddenRequestResult(
                errorMessage: "Receipt can no longer be edited.",
                moreInfo:
                $"Receipts can only be edited within {EditWindow.TotalMinutes:0} minutes of their creation."));

        if (!_isPrivileged && request.PaymentMethod != null)
            return StatusCode(StatusCodes.Status403Forbidden, new ApiResults.ErrorResults.ForbiddenRequestResult(
                errorMessage: "Only admins or financial managers may set the payment method.",
                moreInfo: "The payment method can only be selected by ADMIN or FINANCIAL_MANAGER."));

        if (request.Amount is <= 0)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Amount must be greater than 0."));

        if (request.ReceiptDate == default(DateTime))
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "ReceiptDate must not be empty."));

        if (request.CategoryId != null &&
            !await _db.ReceiptCategories.AnyAsync(c => c.Id == request.CategoryId))
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Category not found."));

        if (request.Amount != null)
            receipt.Amount = request.Amount.Value;
        if (request.ReceiptDate != null)
            receipt.ReceiptDate = request.ReceiptDate.Value;
        if (request.CategoryId != null)
            receipt.CategoryId = request.CategoryId;
        if (request.Vendor != null)
            receipt.Vendor = request.Vendor;
        if (request.Description != null)
            receipt.Description = request.Description;
        if (request.PaymentMethod != null)
            receipt.PaymentMethod = request.PaymentMethod;

        _db.Update(receipt);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(AuditLogActions.UPDATE, nameof(ReceiptEntity),
            new { ReceiptId = id, receipt.Amount, receipt.ReceiptDate });

        Log.Information($"ReceiptsController: UpdateAsync -> Receipt with ID: {receipt.Id} successfully updated.");

        ReceiptEntity updated = await _db.Receipts
            .Include(r => r.User)
            .Include(r => r.Category)
            .Include(r => r.Files)
            .FirstAsync(r => r.Id == id);

        return Ok(MapToResult(updated));
    }

    [Authorize(Roles = $"{nameof(UserRole.ADMIN)},{nameof(UserRole.FINANCIAL_MANAGER)}")]
    [HttpPost("{id}/pay")]
    public async Task<IActionResult> MarkAsPaidAsync([FromRoute] Guid id, [FromBody] MarkReceiptPaidRequest request)
    {
        ReceiptEntity? receipt = await _db.Receipts.FirstOrDefaultAsync(r => r.Id == id);
        if (receipt == null || receipt.DeletedAt != null)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Receipt not found.",
                moreInfo: $"No receipt with the ID {id} could be found."));

        if (receipt.Paid != null)
            return Conflict(new ApiResults.ErrorResults.ConflictResult(
                errorCode: ApiErrorCodes.CONFLICT_RESOURCE_ALREADY_EXISTS,
                errorMessage: "Receipt has already been marked as paid.",
                moreInfo: $"Receipt {id} is already marked as paid."));

        ReceiptPaymentMethod? effectivePaymentMethod = request.PaymentMethod ?? receipt.PaymentMethod;
        if (effectivePaymentMethod == null)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: "PaymentMethod is required to mark a receipt as paid."));

        receipt.PaymentMethod = effectivePaymentMethod;
        receipt.Paid = _timeProvider.GetUtcNow();
        _db.Update(receipt);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(AuditLogActions.UPDATE, nameof(ReceiptEntity),
            new { ReceiptId = id, receipt.PaymentMethod, Paid = true });

        Log.Information($"ReceiptsController: MarkAsPaidAsync -> Receipt with ID: {receipt.Id} marked as paid.");

        return Ok();
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpPost("{id}")]
    public async Task<IActionResult> RestoreAsync([FromRoute] Guid id)
    {
        Log.Information($"ReceiptsController: RestoreAsync -> Try to restore receipt with ID: {id}");

        ReceiptEntity? receipt = await _db.Receipts.FindAsync(id);
        if (receipt == null)
        {
            Log.Warning($"ReceiptsController: RestoreAsync -> Receipt with ID: {id} not found.");
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Receipt not found.",
                moreInfo: $"No receipt with the ID {id} could be found."));
        }

        receipt.DeletedAt = null;
        _db.Update(receipt);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(AuditLogActions.RESTORE, nameof(ReceiptEntity),
            new { ReceiptId = id, receipt.Amount });

        Log.Information($"ReceiptsController: RestoreAsync -> Receipt with ID: {id} successfully restored.");
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] Guid id)
    {
        return await DeleteReceiptAsync(id, hardDelete: false);
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpDelete("{id}/hard")]
    public async Task<IActionResult> HardDeleteAsync([FromRoute] Guid id)
    {
        return await DeleteReceiptAsync(id, hardDelete: true);
    }

    private async Task<IActionResult> DeleteReceiptAsync(Guid id, bool hardDelete)
    {
        ReceiptEntity? receipt = await _db.Receipts.Include(r => r.Files).FirstOrDefaultAsync(r => r.Id == id);
        if (receipt == null)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Receipt not found.",
                moreInfo: $"No receipt with the ID {id} could be found."));

        if (!hardDelete && !_isPrivileged && receipt.UserId != _currentUserId)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Receipt not found.",
                moreInfo: $"No receipt with the ID {id} could be found."));

        _db.Remove(receipt);

        if (hardDelete)
        {
            _receiptService.DeleteFiles(receipt.Files);
            await _db.ForceSaveChangesAsync();
        }
        else
        {
            await _db.SaveChangesAsync();
        }

        await _auditService.LogAsync(hardDelete ? AuditLogActions.DELETE : AuditLogActions.SOFT_DELETE, nameof(ReceiptEntity),
            new { ReceiptId = id, receipt.Amount });

        return Ok();
    }

    private static ReceiptResult MapToResult(ReceiptEntity receipt)
    {
        return new ReceiptResult
        {
            Id = receipt.Id,
            UserId = receipt.UserId,
            UserName = receipt.User?.Username,
            Amount = receipt.Amount,
            ReceiptDate = receipt.ReceiptDate,
            CategoryId = receipt.CategoryId,
            CategoryName = receipt.Category?.Name,
            Vendor = receipt.Vendor,
            Description = receipt.Description,
            PaymentMethod = receipt.PaymentMethod,
            Paid = receipt.Paid != null,
            Files = receipt.Files
                .OrderBy(i => i.Position)
                .Select(i => new ReceiptFileResult() { Id = i.Id, ContentType = i.ContentType, Position = i.Position })
                .ToList(),
            CreatedAt = receipt.CreatedAt,
            DeletedAt = receipt.DeletedAt
        };
    }

    private sealed class ReceiptAnalyticsRow
    {
        public int Year { get; init; }
        public int Month { get; init; }
        public Guid? CategoryId { get; init; }
        public string? CategoryName { get; init; }
        public decimal Amount { get; init; }
    }
}
