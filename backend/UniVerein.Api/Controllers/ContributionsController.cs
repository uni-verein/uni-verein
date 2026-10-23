using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Extensions;
using UniVerein.Api.Helper;
using UniVerein.Api.Models.Enums;
using UniVerein.Api.Query;
using UniVerein.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using Microsoft.AspNetCore.Cors;
using Serilog;

namespace UniVerein.Api.Controllers;

[Authorize]
[ApiController]
[Route("contributions")]
[EnableCors("AllowFrontend")]
public class ContributionsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuditService _auditService;

    public ContributionsController(AppDbContext db, AuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    [HttpGet("info")]
    public async Task<ActionResult<ContributionInfoResult>> GetOpenPaymentInfoAsync()
    {
        IQueryable<decimal> query = _db.Contributions
            .Where(x => x.DeletedAt == null && x.Paid == null)
            .Select(c => c.Amount);

        int count = await query.CountAsync();
        ContributionInfoResult result = new()
        {
            OpenPayments = count,
            OpenAmount = (await query.ToListAsync()).Sum()
        };

        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<AllContributionsResult>> GetAsync([FromQuery] ContributionsQuery contributionsQuery)
    {
        if (contributionsQuery.Offset < 0 || contributionsQuery.Limit < 1)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Offset and/or Limit must be greater than or equal to 1."));

        bool showDeleted = User.FindFirst(ClaimTypes.Role)?.Value == nameof(UserRole.ADMIN)
                           && contributionsQuery.Deleted == true;

        IQueryable<ContributionEntity> baseQuery = _db.Contributions
            .Include(c => c.MemberEntity)
            .Where(x => showDeleted ? x.DeletedAt != null : x.DeletedAt == null);

        if (contributionsQuery.MembershipStatus != null)
            baseQuery = baseQuery.Where(x => x.MemberEntity.MembershipStatus == contributionsQuery.MembershipStatus);

        IQueryable<ContributionResult> query = baseQuery
            .Select(c => new ContributionResult()
            {
                Id = c.Id,
                Name = c.MemberEntity.FirstName + " " + c.MemberEntity.LastName,
                Amount = c.Amount,
                DueDate = c.DueDate,
                Paid = c.Paid != null,
                MemberNumber = c.MemberEntity.MemberNumber,
                MembershipStatus = c.MemberEntity.MembershipStatus,
                DeletedAt = c.DeletedAt
            });

        if (contributionsQuery.Unpaid != null)
            query = query.Where(x => x.Paid != contributionsQuery.Unpaid);

        List<ContributionResult> contributionResults;
        int total = 0;
        if (!string.IsNullOrEmpty(contributionsQuery.Name))
        {
            contributionResults = await query.ToListAsync();
            contributionResults = contributionResults.Select(x => new
            {
                contribution = x,
                score = NameSearchHelper.Score(contributionsQuery.Name, x.Name)
            })
                .Where(x => NameSearchHelper.IsMatch(x.score))
                .OrderByDescending(x => x.score)
                .ThenBy(x => x.contribution.Name)
                .Select(x => x.contribution)
                .ToList();
            total = contributionResults.Count;
        }
        else
        {
            query = query.OrderBy(x => x.Name);
            total = await query.CountAsync();
            contributionResults = await query.ToListAsync();
        }

        AllContributionsResult result = new()
        {
            Items = contributionResults.Skip(contributionsQuery.Offset).Take(contributionsQuery.Limit).ToList(),
            Total = total
        };

        return Ok(result);
    }

    [Authorize(Roles = $"{nameof(UserRole.ADMIN)},{nameof(UserRole.FINANCIAL_MANAGER)}")]
    [HttpPost("{id}")]
    public async Task<IActionResult> MarkAsPaidAsync([FromRoute] Guid id, [FromQuery] bool paid)
    {
        ContributionEntity? contribution = await _db.Contributions.FindAsync(id);
        if (contribution == null)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Contribution config not found.",
                moreInfo: $"Contribution not found. Contribution with ID: {id} not found."));

        contribution.Paid = paid ? DateTimeOffset.UtcNow : null;
        _db.Contributions.Update(contribution);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(AuditLogActions.UPDATE, nameof(ContributionEntity),
            new { ContributionId = id, contribution.MemberId, contribution.Amount, Paid = paid });
        Log.Information("ContributionsController: MarkAsPaidAsync -> Contribution {ContributionId} of member {MemberId} marked as {State} by {ActorId}.",
            id, contribution.MemberId, paid ? "paid" : "unpaid", HttpContext.GetActorId());

        return Ok();
    }

    [Authorize(Roles = $"{nameof(UserRole.ADMIN)},{nameof(UserRole.FINANCIAL_MANAGER)}")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] Guid id)
    {
        ContributionEntity? contribution = await _db.Contributions.FindAsync(id);
        if (contribution == null || contribution.DeletedAt != null)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Contribution not found.",
                moreInfo: $"Contribution not found. Contribution with ID: {id} not found."));

        if (contribution.Paid != null)
            return BadRequest(new ApiResults.ErrorResults.BadRequestResult(moreInfo: "Only open contributions can be deleted."));

        _db.Contributions.Remove(contribution);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(AuditLogActions.SOFT_DELETE, nameof(ContributionEntity),
            new { ContributionId = id, contribution.MemberId, contribution.Amount });
        Log.Information("ContributionsController: DeleteAsync -> Contribution {ContributionId} of member {MemberId} soft-deleted by {ActorId}.",
            id, contribution.MemberId, HttpContext.GetActorId());

        return Ok();
    }

    [Authorize(Roles = nameof(UserRole.ADMIN))]
    [HttpPost("{id}/restore")]
    public async Task<IActionResult> RestoreAsync([FromRoute] Guid id)
    {
        ContributionEntity? contribution = await _db.Contributions.FindAsync(id);
        if (contribution == null)
            return NotFound(new ApiResults.ErrorResults.NotFoundResult(errorCode: ApiErrorCodes.RESOURCE_NOT_FOUND,
                errorMessage: "Contribution not found.",
                moreInfo: $"Contribution not found. Contribution with ID: {id} not found."));

        contribution.DeletedAt = null;
        _db.Contributions.Update(contribution);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(AuditLogActions.RESTORE, nameof(ContributionEntity),
            new { ContributionId = id, contribution.MemberId, contribution.Amount });
        Log.Information("ContributionsController: RestoreAsync -> Contribution {ContributionId} of member {MemberId} restored by {ActorId}.",
            id, contribution.MemberId, HttpContext.GetActorId());

        return Ok();
    }
}
