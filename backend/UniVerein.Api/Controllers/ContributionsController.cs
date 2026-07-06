using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Query;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using Microsoft.AspNetCore.Cors;
using F23.StringSimilarity;

namespace UniVerein.Api.Controllers;

[Authorize]
[ApiController]
[Route("contributions")]
[EnableCors("AllowFrontend")]
public class ContributionsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ContributionsController(AppDbContext db)
    {
        _db = db;
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
        IQueryable<ContributionResult> query = _db.Contributions
            .Include(c => c.MemberEntity)
            .Where(x => x.DeletedAt == null)
            .Select(c => new ContributionResult()
            {
                Id = c.Id,
                Name = c.MemberEntity.FirstName + " " + c.MemberEntity.LastName,
                Amount = c.Amount,
                DueDate = c.DueDate,
                Paid = c.Paid != null
            });

        if (contributionsQuery.Unpaid != null)
            query = query.Where(x => x.Paid != contributionsQuery.Unpaid);

        List<ContributionResult> contributionResults;
        int total = 0;
        if (!string.IsNullOrEmpty(contributionsQuery.Name))
        {
            contributionResults = await query.ToListAsync();
            JaroWinkler jaroWinkler = new();
            contributionResults = contributionResults.Select(x => new
                {
                    contribution = x,
                    similarity = jaroWinkler.Similarity($"{x.Name}".ToLower(), contributionsQuery.Name.ToLower())
                })
                .OrderBy(x => x.similarity)
                .Where(x => x.similarity > 0.75)
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

        return Ok();
    }
}