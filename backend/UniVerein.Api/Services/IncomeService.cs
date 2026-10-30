using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniVerein.Api.ApiResults.Income;
using UniVerein.Api.Helper;
using UniVerein.Api.Models;
using UniVerein.Api.Models.Enums;
using UniVerein.Api.Query;
using Microsoft.EntityFrameworkCore;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Services;

public class IncomeService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;

    public IncomeService(AppDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public static DateTime ToUtcDate(DateTime value)
    {
        DateTime utc = value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value;
        return DateTime.SpecifyKind(utc.Date, DateTimeKind.Utc);
    }

    public async Task<IncomeRowsResult> GetRowsAsync(IncomeQuery query, bool showDeleted)
    {
        IncomeKind? kind = query.CategoryId != null ? IncomeKind.CATEGORY : query.Kind;
        bool includeContributions = kind != IncomeKind.CATEGORY;
        bool includeIncomes = kind != IncomeKind.MEMBERSHIP && query.MembershipStatus == null && query.MemberNumber == null;
        (DateTime? from, DateTime? toExclusive) = GetRange(query.DateFrom, query.DateTo);

        IQueryable<ContributionEntity> contributions = FilterContributions(query.MembershipStatus, query.MemberNumber, query.Unpaid, showDeleted, from, toExclusive);
        IQueryable<IncomeEntity> incomes = FilterIncomes(query.CategoryId, query.Unpaid, showDeleted, from, toExclusive);

        if (!string.IsNullOrWhiteSpace(query.Name))
            return await SearchByNameAsync(query, includeContributions, includeIncomes, contributions, incomes);

        IQueryable<IncomeReference>? references = null;
        if (includeContributions)
        {
            references = contributions.Select(x => new IncomeReference
            {
                Id = x.Id,
                IsManual = false,
                IsPaid = x.Paid != null,
                SortDate = x.DueDate
            });
        }

        if (includeIncomes)
        {
            IQueryable<IncomeReference> incomeReferences = incomes.Select(x => new IncomeReference
            {
                Id = x.Id,
                IsManual = true,
                IsPaid = x.Paid != null,
                SortDate = x.IncomeDate
            });
            references = references == null ? incomeReferences : references.Concat(incomeReferences);
        }

        if (references == null)
            return new IncomeRowsResult();

        int total = await references.CountAsync();
        List<IncomeReference> page = await references
            .OrderBy(x => x.IsPaid)
            .ThenByDescending(x => x.SortDate)
            .ThenBy(x => x.Id)
            .Skip(query.Offset)
            .Take(query.Limit)
            .ToListAsync();

        List<Guid> contributionIds = page.Where(x => !x.IsManual).Select(x => x.Id).ToList();
        List<Guid> incomeIds = page.Where(x => x.IsManual).Select(x => x.Id).ToList();

        Dictionary<Guid, IncomeRowResult> rows = new();
        if (contributionIds.Count > 0)
        {
            List<ContributionEntity> entities = await _db.Contributions
                .Include(x => x.MemberEntity)
                .Where(x => contributionIds.Contains(x.Id))
                .ToListAsync();
            foreach (ContributionEntity entity in entities)
                rows[entity.Id] = ToRow(entity);
        }

        if (incomeIds.Count > 0)
        {
            List<IncomeEntity> entities = await _db.Incomes
                .Include(x => x.Category)
                .Where(x => incomeIds.Contains(x.Id))
                .ToListAsync();
            foreach (IncomeEntity entity in entities)
                rows[entity.Id] = ToRow(entity);
        }

        List<IncomeRowResult> items = page.Where(x => rows.ContainsKey(x.Id)).Select(x => rows[x.Id]).ToList();
        await AttachAcknowledgementNumbersAsync(items);
        await AttachBookingNumbersAsync(items);

        return new IncomeRowsResult
        {
            Items = items,
            Total = total
        };
    }

    public async Task<IncomeYearsResult> GetYearsAsync()
    {
        int currentYear = _timeProvider.GetUtcNow().Year;
        List<DateTime> dates = new();
        foreach (DateTime? date in new[]
                 {
                     await _db.Contributions.MinAsync(x => (DateTime?)x.DueDate),
                     await _db.Contributions.MaxAsync(x => (DateTime?)x.DueDate),
                     await _db.Incomes.MinAsync(x => (DateTime?)x.IncomeDate),
                     await _db.Incomes.MaxAsync(x => (DateTime?)x.IncomeDate)
                 })
        {
            if (date != null)
                dates.Add(date.Value);
        }

        int first = Math.Min(currentYear, dates.Count > 0 ? dates.Min(x => x.Year) : currentYear);
        int last = Math.Max(currentYear, dates.Count > 0 ? dates.Max(x => x.Year) : currentYear);
        return new IncomeYearsResult { Years = Enumerable.Range(first, last - first + 1).Reverse().ToList() };
    }

    public async Task<IncomeSummaryResult> GetSummaryAsync(IncomeSummaryQuery query)
    {
        (DateTime? from, DateTime? toExclusive) = GetRange(query.DateFrom, query.DateTo);

        List<decimal> openContributions = await _db.Contributions
            .Where(x => x.DeletedAt == null && x.Paid == null)
            .Select(x => x.Amount)
            .ToListAsync();
        List<decimal> openIncomes = await _db.Incomes
            .Where(x => x.DeletedAt == null && x.Paid == null)
            .Select(x => x.Amount)
            .ToListAsync();

        IQueryable<ContributionEntity> paidContributionQuery = _db.Contributions
            .Where(x => x.DeletedAt == null && x.Paid != null && x.PaymentDate != null);
        IQueryable<IncomeEntity> paidIncomeQuery = _db.Incomes
            .Where(x => x.DeletedAt == null && x.Paid != null && x.PaymentDate != null);
        if (from != null)
        {
            paidContributionQuery = paidContributionQuery.Where(x => x.PaymentDate >= from);
            paidIncomeQuery = paidIncomeQuery.Where(x => x.PaymentDate >= from);
        }

        if (toExclusive != null)
        {
            paidContributionQuery = paidContributionQuery.Where(x => x.PaymentDate < toExclusive);
            paidIncomeQuery = paidIncomeQuery.Where(x => x.PaymentDate < toExclusive);
        }

        List<decimal> membershipAmounts = await paidContributionQuery
            .Select(x => x.Amount)
            .ToListAsync();
        List<(Guid? CategoryId, decimal Amount)> categoryAmounts = (await paidIncomeQuery
                .Select(x => new { x.CategoryId, x.Amount })
                .ToListAsync())
            .Select(x => (x.CategoryId, x.Amount))
            .ToList();
        Dictionary<Guid, string> categoryNames = await _db.IncomeCategories
            .IgnoreQueryFilters()
            .ToDictionaryAsync(x => x.Id, x => x.Name);

        List<IncomeBreakdownResult> breakdown = new();
        AddBreakdown(breakdown, IncomeKind.MEMBERSHIP, null, null, membershipAmounts);
        foreach (IGrouping<Guid?, (Guid? CategoryId, decimal Amount)> group in categoryAmounts
                     .GroupBy(x => x.CategoryId)
                     .OrderBy(g => g.Key.HasValue ? categoryNames.GetValueOrDefault(g.Key.Value) : null))
        {
            string? name = group.Key.HasValue ? categoryNames.GetValueOrDefault(group.Key.Value) : null;
            AddBreakdown(breakdown, IncomeKind.CATEGORY, group.Key, name, group.Select(x => x.Amount).ToList());
        }

        return new IncomeSummaryResult
        {
            OpenAmount = openContributions.Sum() + openIncomes.Sum(),
            OpenCount = openContributions.Count + openIncomes.Count,
            PaidAmount = breakdown.Sum(x => x.Amount),
            PaidCount = breakdown.Sum(x => x.Count),
            Breakdown = breakdown
        };
    }

    private async Task<IncomeRowsResult> SearchByNameAsync(IncomeQuery query, bool includeContributions, bool includeIncomes,
        IQueryable<ContributionEntity> contributions, IQueryable<IncomeEntity> incomes)
    {
        List<(IncomeRowResult Row, double Score)> matches = new();
        string name = query.Name!;

        if (includeContributions)
        {
            List<ContributionEntity> entities = await contributions.Include(x => x.MemberEntity).ToListAsync();
            foreach (ContributionEntity entity in entities)
            {
                IncomeRowResult row = ToRow(entity);
                double score = NameSearchHelper.Score(name, row.Payer);
                if (NameSearchHelper.IsMatch(score))
                    matches.Add((row, score));
            }
        }

        if (includeIncomes)
        {
            List<IncomeEntity> entities = await incomes.Include(x => x.Category).ToListAsync();
            foreach (IncomeEntity entity in entities)
            {
                IncomeRowResult row = ToRow(entity);
                double score = NameSearchHelper.Score(name, row.Payer, row.Description);
                if (NameSearchHelper.IsMatch(score))
                    matches.Add((row, score));
            }
        }

        List<IncomeRowResult> items = matches
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Row.Paid)
            .ThenByDescending(x => x.Row.DueDate)
            .Select(x => x.Row)
            .Skip(query.Offset)
            .Take(query.Limit)
            .ToList();
        await AttachAcknowledgementNumbersAsync(items);
        await AttachBookingNumbersAsync(items);

        return new IncomeRowsResult
        {
            Items = items,
            Total = matches.Count
        };
    }

    private async Task AttachAcknowledgementNumbersAsync(List<IncomeRowResult> rows)
    {
        List<Guid> ids = rows.Select(x => x.Id).ToList();
        if (ids.Count == 0)
            return;

        List<PaymentAcknowledgementEntity> acknowledgements = await _db.PaymentAcknowledgements
            .Where(x => (x.ContributionId != null && ids.Contains(x.ContributionId.Value))
                        || (x.IncomeId != null && ids.Contains(x.IncomeId.Value)))
            .ToListAsync();
        Dictionary<Guid, string> numbers = new();
        foreach (PaymentAcknowledgementEntity acknowledgement in acknowledgements)
            numbers[acknowledgement.ContributionId ?? acknowledgement.IncomeId!.Value] = acknowledgement.Number;

        foreach (IncomeRowResult row in rows)
            row.AcknowledgementNumber = numbers.GetValueOrDefault(row.Id);
    }

    private async Task AttachBookingNumbersAsync(List<IncomeRowResult> rows)
    {
        List<Guid> ids = rows.Select(x => x.Id).ToList();
        if (ids.Count == 0)
            return;

        List<BookingNumberEntity> bookingNumbers = await _db.BookingNumbers
            .Where(x => (x.ContributionId != null && ids.Contains(x.ContributionId.Value))
                        || (x.IncomeId != null && ids.Contains(x.IncomeId.Value)))
            .ToListAsync();
        Dictionary<Guid, string> numbers = new();
        foreach (BookingNumberEntity bookingNumber in bookingNumbers)
            numbers[bookingNumber.ContributionId ?? bookingNumber.IncomeId!.Value] = bookingNumber.Number;

        foreach (IncomeRowResult row in rows)
            row.BookingNumber = numbers.GetValueOrDefault(row.Id);
    }

    private IQueryable<ContributionEntity> FilterContributions(MembershipStatus? membershipStatus, int? memberNumber, bool? unpaid, bool showDeleted, DateTime? from, DateTime? toExclusive)
    {
        IQueryable<ContributionEntity> query = _db.Contributions
            .Where(x => showDeleted ? x.DeletedAt != null : x.DeletedAt == null);

        if (membershipStatus != null)
            query = query.Where(x => x.MemberEntity.MembershipStatus == membershipStatus);

        if (memberNumber != null)
            query = query.Where(x => x.MemberEntity.MemberNumber == memberNumber);

        if (unpaid == true)
            query = query.Where(x => x.Paid == null);
        else if (unpaid == false)
            query = query.Where(x => x.Paid != null);

        if (from != null)
            query = query.Where(x => x.DueDate >= from);

        if (toExclusive != null)
            query = query.Where(x => x.DueDate < toExclusive);

        return query;
    }

    private IQueryable<IncomeEntity> FilterIncomes(Guid? categoryId, bool? unpaid, bool showDeleted,
        DateTime? from, DateTime? toExclusive)
    {
        IQueryable<IncomeEntity> query = _db.Incomes
            .Where(x => showDeleted ? x.DeletedAt != null : x.DeletedAt == null);

        if (categoryId != null)
            query = query.Where(x => x.CategoryId == categoryId);

        if (unpaid == true)
            query = query.Where(x => x.Paid == null);
        else if (unpaid == false)
            query = query.Where(x => x.Paid != null);

        if (from != null)
            query = query.Where(x => x.IncomeDate >= from);

        if (toExclusive != null)
            query = query.Where(x => x.IncomeDate < toExclusive);

        return query;
    }

    private static (DateTime? From, DateTime? ToExclusive) GetRange(DateTime? dateFrom, DateTime? dateTo)
    {
        DateTime? from = dateFrom != null ? ToUtcDate(dateFrom.Value) : null;
        DateTime? toExclusive = dateTo != null ? ToUtcDate(dateTo.Value).AddDays(1) : null;
        return (from, toExclusive);
    }

    private static void AddBreakdown(List<IncomeBreakdownResult> breakdown, IncomeKind kind, Guid? categoryId,
        string? categoryName, List<decimal> amounts)
    {
        if (amounts.Count == 0)
            return;

        breakdown.Add(new IncomeBreakdownResult
        {
            Kind = kind,
            CategoryId = categoryId,
            CategoryName = categoryName,
            Amount = amounts.Sum(),
            Count = amounts.Count
        });
    }

    private static IncomeRowResult ToRow(ContributionEntity entity)
    {
        return new IncomeRowResult
        {
            Id = entity.Id,
            Source = IncomeSource.CONTRIBUTION,
            Kind = IncomeKind.MEMBERSHIP,
            MembershipStatus = entity.MemberEntity.MembershipStatus,
            Payer = entity.MemberEntity.FirstName + " " + entity.MemberEntity.LastName,
            MemberNumber = entity.MemberEntity.MemberNumber,
            Amount = entity.Amount,
            DueDate = entity.DueDate,
            Paid = entity.Paid != null,
            PaymentDate = entity.PaymentDate,
            DeletedAt = entity.DeletedAt
        };
    }

    public static IncomeRowResult ToRow(IncomeEntity entity)
    {
        return new IncomeRowResult
        {
            Id = entity.Id,
            Source = IncomeSource.MANUAL,
            Kind = IncomeKind.CATEGORY,
            CategoryId = entity.CategoryId,
            CategoryName = entity.Category?.Name,
            Payer = entity.Payer,
            Description = entity.Description,
            Amount = entity.Amount,
            DueDate = entity.IncomeDate,
            Paid = entity.Paid != null,
            PaymentDate = entity.PaymentDate,
            DeletedAt = entity.DeletedAt
        };
    }
}
