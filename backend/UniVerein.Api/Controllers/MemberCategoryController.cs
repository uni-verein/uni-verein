using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using Microsoft.AspNetCore.Cors;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace UniVerein.Api.Controllers;

[Authorize]
[ApiController]
[Route("member-categories")]
[EnableCors("AllowFrontend")]
public class MemberCategoriesController : ControllerBase
{
    private readonly AppDbContext _db;

    public MemberCategoriesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<MemberCategoryResults>> GetAllAsync()
    {
        List<MemberCategoryResult> results = await _db.MemberCategories.Select(c => new MemberCategoryResult()
        {
            Id = c.Id,
            Category = c.Category,
            Name = c.Name
        }).ToListAsync();

        return Ok(new MemberCategoryResults()
        {
            Items = results,
            Total = results.Count
        });
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpPost]
    public async Task<ActionResult<MemberCategoryResult>> CreateAsync([FromBody] MemberCategoryRequest request)
    {
        Log.Information(
            $"MemberCategoriesController: CreateAsync -> Try to create a MemberCategory. Request: {JsonSerializer.Serialize(request)}");

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 50)
        {
            Log.Warning(
                $"MemberCategoriesController: CreateAsync -> Name length must be less long then 51 characters. Request: {JsonSerializer.Serialize(request)}");
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Name length must be less long then 51 characters."));
        }

        if (string.IsNullOrWhiteSpace(request.Category) || request.Category.Length > 50)
        {
            Log.Warning(
                $"MemberCategoriesController: CreateAsync -> Category length must be less long then 51 characters. Request: {JsonSerializer.Serialize(request)}");
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Category length must be less long then 51 characters."));
        }

        MemberCategoryEntity? memberCategory = await _db.MemberCategories.FirstOrDefaultAsync(c => c.Name == request.Name && c.Category == request.Category);
        
        if (memberCategory != null && memberCategory.DeletedAt == null)
        {
            Log.Warning(
                $"MemberCategoriesController: CreateAsync -> Member category already exists. Request: {JsonSerializer.Serialize(request)}");
            return Conflict(new ApiResults.ErrorResults.ConflictResult(errorCode: ApiErrorCodes.CONFLICT_RESOURCE_ALREADY_EXISTS,
                errorMessage: "Member category already exists.",
                moreInfo: "Member category with same name and category already exists. Try a other name and category."));
        }

        if (memberCategory != null && memberCategory.DeletedAt != null)
        {
            memberCategory.DeletedAt = null;
            _db.MemberCategories.Update(memberCategory);
        }
        else
        {
            memberCategory = new()
            {
                Name = request.Name,
                Category = request.Category
            };

            await _db.MemberCategories.AddAsync(memberCategory);
        }
        
        await _db.SaveChangesAsync();

        Log.Information(
            $"MemberCategoriesController: CreateAsync -> MemberCategory({request.Category}) successfully created.");
        return Created("", new MemberCategoryResult()
        {
            Id = memberCategory.Id,
            Name = memberCategory.Name,
            Category = memberCategory.Category
        });
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpPut("{id}")]
    public async Task<ActionResult<ContributionPlanResult>> UpdateAsync([FromRoute] Guid id,
        [FromBody] MemberCategoryUpdateRequest request)
    {
        Log.Information(
            $"MemberCategoriesController: UpdateAsync -> Try to update memberCategory with ID: {id}. Request: {JsonSerializer.Serialize(request)}");

        if (!string.IsNullOrWhiteSpace(request.Name) && request.Name.Length > 50)
        {
            Log.Warning(
                $"MemberCategoriesController: UpdateAsync -> For memberCategory with ID: {id} -> Name length must be less long then 51 characters. Request: {JsonSerializer.Serialize(request)}");
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Name length must be less long then 51 characters."));
        }

        if (!string.IsNullOrWhiteSpace(request.Category) && request.Category.Length > 50)
        {
            Log.Warning(
                $"MemberCategoriesController: UpdateAsync -> For memberCategory with ID: {id} -> Category length must be less long then 51 characters. Request: {JsonSerializer.Serialize(request)}");
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Category length must be less long then 51 characters."));
        }

        if (await _db.MemberCategories.AnyAsync(x =>
                x.Id != id && x.Name == request.Name && x.Category == request.Category))
        {
            Log.Warning(
                $"MemberCategoriesController: UpdateAsync -> For memberCategory with ID: {id} -> Member category already exists. Request: {JsonSerializer.Serialize(request)}");
            return Conflict(new ApiResults.ErrorResults.ConflictResult(errorCode: ApiErrorCodes.CONFLICT_RESOURCE_ALREADY_EXISTS,
                errorMessage: "Member category already exists.",
                moreInfo: "Member category with same name and category already exists. Try a other name and category."));
        }

        MemberCategoryEntity? memberCategory = await _db.MemberCategories.FindAsync(id);
        if (memberCategory == null)
        {
            Log.Warning(
                $"MemberCategoriesController: UpdateAsync -> For memberCategory with ID: {id} -> Member category not found. Request: {JsonSerializer.Serialize(request)}");
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Member category not found.",
                moreInfo: "Member category with given ID not found. Try a other member category ID."));
        }

        if (!string.IsNullOrWhiteSpace(request.Name) && !string.IsNullOrWhiteSpace(request.Category))
        {
            memberCategory.Name = request.Name;
            memberCategory.Category = request.Category;
        }

        _db.MemberCategories.Update(memberCategory);
        await _db.SaveChangesAsync();

        Log.Information(
            $"MemberCategoriesController: UpdateAsync -> Member category with ID: {id} successfully updated.");
        return Ok(new MemberCategoryResult()
        {
            Id = memberCategory.Id,
            Category = memberCategory.Category,
            Name = memberCategory.Name,
        });
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] Guid id)
    {
        MemberCategoryEntity? memberCategory = await _db.MemberCategories.FindAsync(id);
        if (memberCategory == null)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Member category not found.",
                moreInfo: $"Member category with ID {id} not found."));

        if (await _db.Members.AnyAsync(x => x.MemberCategoryId == memberCategory.Id && x.DeletedAt == null))
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: "Member category could not be deleted because member category is assigned to a member."));

        _db.Remove(memberCategory);
        await _db.SaveChangesAsync();
        return Ok();
    }
}