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
[Route("contribution-plans")]
[EnableCors("AllowFrontend")]
public class ContributionPlansController : ControllerBase
{
    private readonly AppDbContext _db;

    public ContributionPlansController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<ContributionPlanResults>> GetAllAsync()
    {
        List<ContributionPlanResult> results = await _db.ContributionPlans.Select(c => new ContributionPlanResult()
        {
            Id = c.Id,
            Name = c.Name,
            Amount = c.Amount,
            Interval = c.Interval
        }).ToListAsync();

        return Ok(new ContributionPlanResults()
        {
            Items = results,
            Total = results.Count
        });
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpPost]
    public async Task<ActionResult<ContributionPlanResult>> CreateAsync([FromBody] ContributionPlanRequest request)
    {
        Log.Information(
            $"ContributionPlansController: CreateAsync -> Try to create a contributionPlan. Request: {JsonSerializer.Serialize(request)}");

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 50)
        {
            Log.Warning(
                $"ContributionPlansController: CreateAsync -> Name length must be less long then 51 characters. Request: {JsonSerializer.Serialize(request)}");
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Name length must be less long then 51 characters."));
        }

        if (request.Amount < 0)
        {
            Log.Warning(
                $"ContributionPlansController: CreateAsync -> Amount must be greater then 0. Request: {JsonSerializer.Serialize(request)}");
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Amount must be greater then 0."));
        }

        if (await _db.ContributionPlans.AnyAsync(u => u.Name == request.Name && u.Amount == request.Amount))
        {
            Log.Warning(
                $"ContributionPlansController: CreateAsync -> Contribution plan already exists. Request: {JsonSerializer.Serialize(request)}");
            return Conflict(new ApiResults.ErrorResults.ConflictResult(errorCode: ApiErrorCodes.CONFLICT_RESOURCE_ALREADY_EXISTS,
                errorMessage: "Contribution plan already exists.",
                moreInfo: "Contribution plan with same name and amount already exists. Try a other name or amount."));
        }

        ContributionPlanEntity contributionPlan = new()
        {
            Name = request.Name,
            Amount = request.Amount,
            Interval = request.Interval
        };

        await _db.ContributionPlans.AddAsync(contributionPlan);
        await _db.SaveChangesAsync();

        Log.Information(
            $"ContributionPlansController: CreateAsync -> ContributionPlan({request.Name}) successfully created.");
        return Created("", new ContributionPlanResult()
        {
            Id = contributionPlan.Id,
            Name = contributionPlan.Name,
            Amount = contributionPlan.Amount
        });
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpPatch("{id}")]
    public async Task<ActionResult<ContributionPlanResult>> UpdateAsync([FromRoute] Guid id, [FromBody] ContributionPlanUpdateRequest request)
    {
        Log.Information(
            $"ContributionPlansController: UpdateAsync -> Try to update contributionPlan with ID: {id}. Request: {JsonSerializer.Serialize(request)}");

        if (string.IsNullOrWhiteSpace(request.Name) && request.Amount == null && request.Interval == null)
        {
            Log.Warning(
                $"ContributionPlansController: UpdateAsync -> For contributionPlan with ID: {id} -> Nothing to update. Request: {JsonSerializer.Serialize(request)}");
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Nothing to update."));
        }

        if (!string.IsNullOrWhiteSpace(request.Name) && request.Name.Length > 50)
        {
            Log.Warning(
                $"ContributionPlansController: UpdateAsync -> For contributionPlan with ID: {id} -> Name length must be less long then 51 characters. Request: {JsonSerializer.Serialize(request)}");
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Name length must be less long then 51 characters."));
        }

        if (request.Amount != null && request.Amount < 0)
        {
            Log.Warning(
                $"ContributionPlansController: UpdateAsync -> For contributionPlan with ID: {id} -> Amount must be greater then 0. Request: {JsonSerializer.Serialize(request)}");
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Amount must be greater then 0."));
        }

        if (await _db.ContributionPlans.AnyAsync(x =>
                x.Id != id && x.Name == request.Name && x.Amount == request.Amount))
        {
            Log.Warning(
                $"ContributionPlansController: UpdateAsync -> For contributionPlan with ID: {id} -> Contribution plan already exists. Request: {JsonSerializer.Serialize(request)}");
            return Conflict(new ApiResults.ErrorResults.ConflictResult(errorCode: ApiErrorCodes.CONFLICT_RESOURCE_ALREADY_EXISTS,
                errorMessage: "Contribution plan already exists.",
                moreInfo: "Contribution plan with same name and amount already exists. Try a other name or amount."));
        }

        ContributionPlanEntity? contributionPlan = await _db.ContributionPlans.FindAsync(id);
        if (contributionPlan == null)
        {
            Log.Warning(
                $"ContributionPlansController: UpdateAsync -> For contributionPlan with ID: {id} -> Contribution plan not found. Request: {JsonSerializer.Serialize(request)}");
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Contribution plan not found.",
                moreInfo: $"Contribution plan with given ID not found. Try a other contribution plan ID."));
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
            contributionPlan.Name = request.Name;
        if (request.Amount != null)
            contributionPlan.Amount = (decimal)request.Amount!;
        if (request.Interval != null)
            contributionPlan.Interval = (Interval)request.Interval!;

        _db.ContributionPlans.Update(contributionPlan);
        await _db.SaveChangesAsync();

        Log.Information(
            $"ContributionPlansController: UpdateAsync -> ContributionPlan with ID: {id} successfully updated.");
        return Ok(new ContributionPlanResult()
        {
            Id = contributionPlan.Id,
            Name = contributionPlan.Name,
            Amount = contributionPlan.Amount,
            Interval = contributionPlan.Interval
        });
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] Guid id)
    {
        ContributionPlanEntity? contributionPlan = await _db.ContributionPlans.FindAsync(id);
        if (contributionPlan == null)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Contribution plan not found.",
                moreInfo: $"Contribution plan with ID {id} not found."));

        if (await _db.Members.AnyAsync(x => x.ContributionPlanId == contributionPlan.Id && x.DeletedAt == null))
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(
                moreInfo: "Contribution plan could not be deleted because contribution plan is assigned to a member."));

        _db.Remove(contributionPlan);
        await _db.SaveChangesAsync();
        return Ok();
    }
}