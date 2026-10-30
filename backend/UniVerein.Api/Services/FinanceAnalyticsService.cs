using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UniVerein.Api.ApiResults.Finance;
using UniVerein.Api.Models.Enums;
using UniVerein.DAL.Data;

namespace UniVerein.Api.Services;

public class FinanceAnalyticsService
{
    private readonly AppDbContext _db;

    public FinanceAnalyticsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<FinanceAnalyticsResult> GetAnalyticsAsync()
    {
        List<FinanceAnalyticsRowResult> membership = await _db.Contributions
            .Where(x => x.DeletedAt == null && x.Paid != null && x.PaymentDate != null)
            .GroupBy(x => new { x.PaymentDate!.Value.Year, x.PaymentDate!.Value.Month })
            .Select(g => new FinanceAnalyticsRowResult
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Kind = FinanceEntryKind.MEMBERSHIP,
                Amount = g.Sum(x => x.Amount)
            })
            .ToListAsync();

        List<FinanceAnalyticsRowResult> income = await _db.Incomes
            .Where(x => x.DeletedAt == null && x.Paid != null && x.PaymentDate != null)
            .GroupBy(x => new
            {
                x.PaymentDate!.Value.Year,
                x.PaymentDate!.Value.Month,
                x.CategoryId,
                CategoryName = x.Category != null ? x.Category.Name : null
            })
            .Select(g => new FinanceAnalyticsRowResult
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Kind = FinanceEntryKind.INCOME,
                CategoryId = g.Key.CategoryId,
                CategoryName = g.Key.CategoryName,
                Amount = g.Sum(x => x.Amount)
            })
            .ToListAsync();

        List<FinanceAnalyticsRowResult> expenses = await _db.Receipts
            .Where(x => x.DeletedAt == null && x.Paid != null && x.PaymentDate != null)
            .GroupBy(x => new
            {
                x.PaymentDate!.Value.Year,
                x.PaymentDate!.Value.Month,
                x.CategoryId,
                CategoryName = x.Category != null ? x.Category.Name : null
            })
            .Select(g => new FinanceAnalyticsRowResult
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Kind = FinanceEntryKind.EXPENSE,
                CategoryId = g.Key.CategoryId,
                CategoryName = g.Key.CategoryName,
                Amount = g.Sum(x => x.Amount)
            })
            .ToListAsync();

        return new FinanceAnalyticsResult
        {
            Rows = membership.Concat(income).Concat(expenses)
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .ThenBy(x => x.Kind)
                .ThenByDescending(x => x.Amount)
                .ToList()
        };
    }
}
