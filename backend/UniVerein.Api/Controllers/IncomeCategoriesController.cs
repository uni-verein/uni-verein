using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.ApiResults.Income;
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

namespace UniVerein.Api.Controllers;

[Authorize]
[ApiController]
[Route("income-categories")]
[EnableCors("AllowFrontend")]
public class IncomeCategoriesController : ControllerBase
{
    private const int MaxNameLength = 50;

    private readonly AppDbContext _db;

    public IncomeCategoriesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IncomeCategoryResults>> GetAllAsync()
    {
        List<IncomeCategoryResult> results = await _db.IncomeCategories
            .OrderBy(c => c.Name)
            .Select(c => new IncomeCategoryResult() { Id = c.Id, Name = c.Name, TaxArea = c.TaxArea })
            .ToListAsync();

        return Ok(new IncomeCategoryResults() { Items = results, Total = results.Count });
    }

    [Authorize(Roles = $"{nameof(UserRole.ADMIN)},{nameof(UserRole.FINANCIAL_MANAGER)}")]
    [HttpPost]
    public async Task<ActionResult<IncomeCategoryResult>> CreateAsync([FromBody] IncomeCategoryRequest request)
    {
        Log.Information("IncomeCategoriesController: CreateAsync -> Try to create an income category.");

        string name = request.Name.Trim();
        if (name.Length == 0 || name.Length > MaxNameLength)
        {
            Log.Warning("IncomeCategoriesController: CreateAsync -> Name length must be between 1 and {MaxLength} characters.", MaxNameLength);
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: $"Name length must be between 1 and {MaxNameLength} characters."));
        }

        if (request.TaxArea != null && !Enum.IsDefined(request.TaxArea.Value))
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Unknown tax area."));

        IncomeCategoryEntity? existingCategory = await _db.IncomeCategories
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Name == name);

        if (existingCategory != null)
        {
            if (existingCategory.DeletedAt == null)
            {
                Log.Warning("IncomeCategoriesController: CreateAsync -> Income category already exists.");
                return Conflict(new ApiResults.ErrorResults.ConflictResult(
                    errorCode: ApiErrorCodes.CONFLICT_RESOURCE_ALREADY_EXISTS,
                    errorMessage: "Income category already exists.",
                    moreInfo: "Income category with same name already exists. Try a other name."));
            }

            Log.Information("IncomeCategoriesController: CreateAsync -> Restored soft-deleted income category with ID: {CategoryId}.", existingCategory.Id);
            existingCategory.DeletedAt = null;
            existingCategory.TaxArea = request.TaxArea ?? existingCategory.TaxArea;
            _db.Update(existingCategory);
            await _db.SaveChangesAsync();

            return Created("", ToResult(existingCategory));
        }

        IncomeCategoryEntity category = new() { Name = name, TaxArea = request.TaxArea ?? TaxArea.NON_PROFIT };
        await _db.IncomeCategories.AddAsync(category);
        await _db.SaveChangesAsync();

        Log.Information("IncomeCategoriesController: CreateAsync -> Income category with ID: {CategoryId} successfully created.", category.Id);

        return Created("", ToResult(category));
    }

    [Authorize(Roles = $"{nameof(UserRole.ADMIN)},{nameof(UserRole.FINANCIAL_MANAGER)}")]
    [HttpPut("{id}")]
    public async Task<ActionResult<IncomeCategoryResult>> UpdateAsync([FromRoute] Guid id,
        [FromBody] IncomeCategoryRequest request)
    {
        Log.Information("IncomeCategoriesController: UpdateAsync -> Try to update income category with ID: {CategoryId}", id);

        IncomeCategoryEntity? category = await _db.IncomeCategories.FindAsync(id);
        if (category == null)
        {
            Log.Warning("IncomeCategoriesController: UpdateAsync -> Income category with ID: {CategoryId} not found.", id);
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Income category not found.",
                moreInfo: $"Income category with ID {id} not found."));
        }

        string name = request.Name.Trim();
        if (name.Length == 0 || name.Length > MaxNameLength)
        {
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: $"Name length must be between 1 and {MaxNameLength} characters."));
        }

        if (request.TaxArea != null && !Enum.IsDefined(request.TaxArea.Value))
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Unknown tax area."));

        if (await _db.IncomeCategories.AnyAsync(c => c.Id != id && c.Name == name))
        {
            return Conflict(new ApiResults.ErrorResults.ConflictResult(
                errorCode: ApiErrorCodes.CONFLICT_RESOURCE_ALREADY_EXISTS,
                errorMessage: "Income category already exists.",
                moreInfo: "Income category with same name already exists. Try a other name."));
        }

        category.Name = name;
        category.TaxArea = request.TaxArea ?? category.TaxArea;
        await _db.SaveChangesAsync();

        Log.Information("IncomeCategoriesController: UpdateAsync -> Income category with ID: {CategoryId} successfully updated.", id);

        return Ok(ToResult(category));
    }

    [Authorize(Roles = $"{nameof(UserRole.ADMIN)},{nameof(UserRole.FINANCIAL_MANAGER)}")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] Guid id)
    {
        Log.Information("IncomeCategoriesController: DeleteAsync -> Try to delete income category with ID: {CategoryId}", id);

        IncomeCategoryEntity? category = await _db.IncomeCategories.FindAsync(id);
        if (category == null)
        {
            Log.Warning("IncomeCategoriesController: DeleteAsync -> Income category with ID: {CategoryId} not found.", id);
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Income category not found.",
                moreInfo: $"Income category with ID {id} not found."));
        }

        if (await _db.Incomes.AnyAsync(i => i.CategoryId == category.Id && i.DeletedAt == null))
        {
            Log.Warning("IncomeCategoriesController: DeleteAsync -> Income category with ID: {CategoryId} is still assigned to an income.", id);
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: "Income category could not be deleted because it is assigned to an income."));
        }

        _db.Remove(category);
        await _db.SaveChangesAsync();

        Log.Information("IncomeCategoriesController: DeleteAsync -> Income category with ID: {CategoryId} successfully deleted.", id);

        return Ok();
    }

    private static IncomeCategoryResult ToResult(IncomeCategoryEntity category)
    {
        return new IncomeCategoryResult() { Id = category.Id, Name = category.Name, TaxArea = category.TaxArea };
    }
}
