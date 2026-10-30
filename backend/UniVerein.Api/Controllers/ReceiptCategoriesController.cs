using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using System;
using Serilog;
using UniVerein.Api.ApiResults.Receipt;

namespace UniVerein.Api.Controllers;

[Authorize]
[ApiController]
[Route("receipt-categories")]
[EnableCors("AllowFrontend")]
public class ReceiptCategoriesController : ControllerBase
{
    private readonly AppDbContext _db;

    public ReceiptCategoriesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<ReceiptCategoryResults>> GetAllAsync()
    {
        List<ReceiptCategoryResult> results = await _db.ReceiptCategories
            .OrderBy(c => c.Name)
            .Select(c => new ReceiptCategoryResult() { Id = c.Id, Name = c.Name, TaxArea = c.TaxArea })
            .ToListAsync();

        return Ok(new ReceiptCategoryResults() { Items = results, Total = results.Count });
    }

    [Authorize(Roles = $"{nameof(UserRole.ADMIN)},{nameof(UserRole.FINANCIAL_MANAGER)}")]
    [HttpPost]
    public async Task<ActionResult<ReceiptCategoryResult>> CreateAsync([FromBody] ReceiptCategoryRequest request)
    {
        Log.Information($"ReceiptCategoriesController: CreateAsync -> Try to create a receipt category: {request.Name}");

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 50)
        {
            Log.Warning("ReceiptCategoriesController: CreateAsync -> Name length must be less long then 51 characters.");
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: "Name length must be less long then 51 characters."));
        }

        if (request.TaxArea != null && !Enum.IsDefined(request.TaxArea.Value))
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Unknown tax area."));

        ReceiptCategoryEntity? existingCategory = await _db.ReceiptCategories
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Name == request.Name);

        if (existingCategory != null)
        {
            if (existingCategory.DeletedAt == null)
            {
                Log.Warning($"ReceiptCategoriesController: CreateAsync -> Receipt category already exists: {request.Name}");
                return Conflict(new ApiResults.ErrorResults.ConflictResult(
                    errorCode: ApiErrorCodes.CONFLICT_RESOURCE_ALREADY_EXISTS,
                    errorMessage: "Receipt category already exists.",
                    moreInfo: "Receipt category with same name already exists. Try a other name."));
            }

            Log.Information($"ReceiptCategoriesController: CreateAsync -> Restored soft-deleted receipt category with ID: {existingCategory.Id}.");
            existingCategory.DeletedAt = null;
            existingCategory.TaxArea = request.TaxArea ?? existingCategory.TaxArea;
            _db.Update(existingCategory);
            await _db.SaveChangesAsync();

            return Created("", ToResult(existingCategory));
        }

        ReceiptCategoryEntity category = new() { Name = request.Name, TaxArea = request.TaxArea ?? TaxArea.NON_PROFIT };
        await _db.ReceiptCategories.AddAsync(category);
        await _db.SaveChangesAsync();

        Log.Information($"ReceiptCategoriesController: CreateAsync -> Receipt category with ID: {category.Id} successfully created.");

        return Created("", ToResult(category));
    }

    [Authorize(Roles = $"{nameof(UserRole.ADMIN)},{nameof(UserRole.FINANCIAL_MANAGER)}")]
    [HttpPut("{id}")]
    public async Task<ActionResult<ReceiptCategoryResult>> UpdateAsync([FromRoute] Guid id,
        [FromBody] ReceiptCategoryRequest request)
    {
        Log.Information($"ReceiptCategoriesController: UpdateAsync -> Try to update receipt category with ID: {id}");

        ReceiptCategoryEntity? category = await _db.ReceiptCategories.FindAsync(id);
        if (category == null)
        {
            Log.Warning($"ReceiptCategoriesController: UpdateAsync -> Receipt category with ID: {id} not found.");
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Receipt category not found.",
                moreInfo: $"Receipt category with ID {id} not found."));
        }

        string name = request.Name.Trim();
        if (name.Length == 0 || name.Length > 50)
        {
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: "Name length must be between 1 and 50 characters."));
        }

        if (request.TaxArea != null && !Enum.IsDefined(request.TaxArea.Value))
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Unknown tax area."));

        if (await _db.ReceiptCategories.AnyAsync(c => c.Id != id && c.Name == name))
        {
            return Conflict(new ApiResults.ErrorResults.ConflictResult(
                errorCode: ApiErrorCodes.CONFLICT_RESOURCE_ALREADY_EXISTS,
                errorMessage: "Receipt category already exists.",
                moreInfo: "Receipt category with same name already exists. Try a other name."));
        }

        category.Name = name;
        category.TaxArea = request.TaxArea ?? category.TaxArea;
        await _db.SaveChangesAsync();

        Log.Information($"ReceiptCategoriesController: UpdateAsync -> Receipt category with ID: {id} successfully updated.");

        return Ok(ToResult(category));
    }

    [Authorize(Roles = $"{nameof(UserRole.ADMIN)},{nameof(UserRole.FINANCIAL_MANAGER)}")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] Guid id)
    {
        Log.Information($"ReceiptCategoriesController: DeleteAsync -> Try to delete receipt category with ID: {id}");

        ReceiptCategoryEntity? category = await _db.ReceiptCategories.FindAsync(id);
        if (category == null)
        {
            Log.Warning($"ReceiptCategoriesController: DeleteAsync -> Receipt category with ID: {id} not found.");
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Receipt category not found.",
                moreInfo: $"Receipt category with ID {id} not found."));
        }

        if (await _db.Receipts.AnyAsync(r => r.CategoryId == category.Id && r.DeletedAt == null))
        {
            Log.Warning($"ReceiptCategoriesController: DeleteAsync -> Receipt category with ID: {id} is still assigned to a receipt.");
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: "Receipt category could not be deleted because it is assigned to a receipt."));
        }

        _db.Remove(category);
        await _db.SaveChangesAsync();

        Log.Information($"ReceiptCategoriesController: DeleteAsync -> Receipt category with ID: {id} successfully deleted.");

        return Ok();
    }

    private static ReceiptCategoryResult ToResult(ReceiptCategoryEntity category)
    {
        return new ReceiptCategoryResult() { Id = category.Id, Name = category.Name, TaxArea = category.TaxArea };
    }
}
