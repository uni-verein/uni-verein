using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults.Calendar;
using UniVerein.Api.Exceptions;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Controllers;

[Authorize]
[ApiController]
[Route("calendar/categories")]
[EnableCors("AllowFrontend")]
public class CalendarCategoriesController : ControllerBase
{
    private readonly AppDbContext _db;

    public CalendarCategoriesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<CalendarCategoryResults>> GetAllAsync()
    {
        List<CalendarCategoryResult> results = await _db.CalendarCategories
            .OrderBy(c => c.Name)
            .Select(c => ToResult(c))
            .ToListAsync();

        return Ok(new CalendarCategoryResults { Items = results, Total = results.Count });
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpPost]
    public async Task<ActionResult<CalendarCategoryResult>> CreateAsync([FromBody] CalendarCategoryRequest request)
    {
        Log.Information($"CalendarCategoriesController: CreateAsync -> Try to create a calendar category: {request.Name}");

        string name = request.Name.Trim();
        CalendarCategoryEntity? existingCategory = await _db.CalendarCategories
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Name == name);

        if (existingCategory != null)
        {
            if (existingCategory.DeletedAt == null)
                return CategoryAlreadyExists(name);

            existingCategory.DeletedAt = null;
            existingCategory.Color = request.Color;
            existingCategory.IsPublic = request.IsPublic;
            await _db.SaveChangesAsync();

            Log.Information($"CalendarCategoriesController: CreateAsync -> Restored soft-deleted calendar category with ID: {existingCategory.Id}.");
            return Created("", ToResult(existingCategory));
        }

        CalendarCategoryEntity category = new() { Name = name, Color = request.Color, IsPublic = request.IsPublic };
        await _db.CalendarCategories.AddAsync(category);
        await _db.SaveChangesAsync();

        Log.Information($"CalendarCategoriesController: CreateAsync -> Calendar category with ID: {category.Id} successfully created.");
        return Created("", ToResult(category));
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpPut("{id}")]
    public async Task<ActionResult<CalendarCategoryResult>> UpdateAsync([FromRoute] Guid id,
        [FromBody] CalendarCategoryRequest request)
    {
        Log.Information($"CalendarCategoriesController: UpdateAsync -> Try to update calendar category with ID: {id}");

        CalendarCategoryEntity? category = await _db.CalendarCategories.FindAsync(id);
        if (category == null)
            return CategoryNotFound(id);

        string name = request.Name.Trim();
        if (await _db.CalendarCategories.AnyAsync(c => c.Id != id && c.Name == name))
            return CategoryAlreadyExists(name);

        category.Name = name;
        category.Color = request.Color;
        category.IsPublic = request.IsPublic;
        await _db.SaveChangesAsync();

        return Ok(ToResult(category));
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] Guid id)
    {
        Log.Information($"CalendarCategoriesController: DeleteAsync -> Try to delete calendar category with ID: {id}");

        CalendarCategoryEntity? category = await _db.CalendarCategories.FindAsync(id);
        if (category == null)
            return CategoryNotFound(id);

        // Events keep existing without a category instead of blocking the deletion.
        List<CalendarEventEntity> events = await _db.CalendarEvents
            .IgnoreQueryFilters()
            .Where(e => e.CategoryId == id)
            .ToListAsync();
        foreach (CalendarEventEntity calendarEvent in events)
            calendarEvent.CategoryId = null;

        _db.Remove(category);
        await _db.SaveChangesAsync();

        Log.Information($"CalendarCategoriesController: DeleteAsync -> Calendar category with ID: {id} successfully deleted.");
        return NoContent();
    }

    private static CalendarCategoryResult ToResult(CalendarCategoryEntity category)
    {
        return new CalendarCategoryResult
        {
            Id = category.Id,
            Name = category.Name,
            Color = category.Color,
            IsPublic = category.IsPublic
        };
    }

    private NotFoundObjectResult CategoryNotFound(Guid id)
    {
        Log.Warning($"CalendarCategoriesController: Calendar category with ID: {id} not found.");
        return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
            errorMessage: "Calendar category not found.",
            moreInfo: $"Calendar category with ID {id} not found."));
    }

    private ConflictObjectResult CategoryAlreadyExists(string name)
    {
        Log.Warning($"CalendarCategoriesController: Calendar category already exists: {name}");
        return Conflict(new ApiResults.ErrorResults.ConflictResult(
            errorCode: ApiErrorCodes.CONFLICT_RESOURCE_ALREADY_EXISTS,
            errorMessage: "Calendar category already exists.",
            moreInfo: "Calendar category with same name already exists. Try a other name."));
    }
}
