using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniVerein.Api.ApiResults;
using UniVerein.DAL.Data;
using Microsoft.EntityFrameworkCore;

namespace UniVerein.Api.Services;

public class ReferenceDataService
{
    private readonly AppDbContext _db;

    public ReferenceDataService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<MemberCategoryResult>> GetMemberCategoriesAsync()
    {
        return await _db.MemberCategories.Select(c => new MemberCategoryResult()
        {
            Id = c.Id,
            Category = c.Category,
            Name = c.Name
        }).ToListAsync();
    }

    public async Task<List<ContributionPlanResult>> GetContributionPlansAsync()
    {
        return await _db.ContributionPlans.Select(c => new ContributionPlanResult()
        {
            Id = c.Id,
            Name = c.Name,
            Amount = c.Amount,
            Interval = c.Interval
        }).ToListAsync();
    }
}
