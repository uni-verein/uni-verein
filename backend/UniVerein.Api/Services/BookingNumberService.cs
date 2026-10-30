using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Services;

public class BookingNumberService
{
    private const int MaxNumberingAttempts = 3;

    private readonly AppDbContext _db;

    public BookingNumberService(AppDbContext db)
    {
        _db = db;
    }

    public Task<BookingNumberEntity?> TryAssignContributionAsync(ContributionEntity contribution, DateTime paymentDate)
    {
        return TryAssignAsync(BookingKind.INCOME, paymentDate.Year, x => x.ContributionId == contribution.Id,
            id => id.ContributionId = contribution.Id);
    }

    public Task<BookingNumberEntity?> TryAssignIncomeAsync(IncomeEntity income, DateTime paymentDate)
    {
        return TryAssignAsync(BookingKind.INCOME, paymentDate.Year, x => x.IncomeId == income.Id,
            id => id.IncomeId = income.Id);
    }

    public Task<BookingNumberEntity?> TryAssignReceiptAsync(ReceiptEntity receipt, DateTime paymentDate)
    {
        return TryAssignAsync(BookingKind.EXPENSE, paymentDate.Year, x => x.ReceiptId == receipt.Id,
            id => id.ReceiptId = receipt.Id);
    }

    public async Task AssignMissingAsync()
    {
        List<(BookingKind Kind, DateTime PaymentDate, DateTimeOffset CreatedAt, Guid Id, Action<BookingNumberEntity> Bind)> pending = new();

        var contributions = await _db.Contributions
            .Where(x => x.Paid != null && x.PaymentDate != null && !_db.BookingNumbers.Any(b => b.ContributionId == x.Id))
            .Select(x => new { x.Id, PaymentDate = x.PaymentDate!.Value, x.CreatedAt })
            .ToListAsync();
        pending.AddRange(contributions.Select(x => (BookingKind.INCOME, x.PaymentDate, x.CreatedAt, x.Id,
            (Action<BookingNumberEntity>)(n => n.ContributionId = x.Id))));

        var incomes = await _db.Incomes
            .Where(x => x.Paid != null && x.PaymentDate != null && !_db.BookingNumbers.Any(b => b.IncomeId == x.Id))
            .Select(x => new { x.Id, PaymentDate = x.PaymentDate!.Value, x.CreatedAt })
            .ToListAsync();
        pending.AddRange(incomes.Select(x => (BookingKind.INCOME, x.PaymentDate, x.CreatedAt, x.Id,
            (Action<BookingNumberEntity>)(n => n.IncomeId = x.Id))));

        var receipts = await _db.Receipts
            .Where(x => x.Paid != null && x.PaymentDate != null && !_db.BookingNumbers.Any(b => b.ReceiptId == x.Id))
            .Select(x => new { x.Id, PaymentDate = x.PaymentDate!.Value, x.CreatedAt })
            .ToListAsync();
        pending.AddRange(receipts.Select(x => (BookingKind.EXPENSE, x.PaymentDate, x.CreatedAt, x.Id,
            (Action<BookingNumberEntity>)(n => n.ReceiptId = x.Id))));

        foreach (IGrouping<(BookingKind Kind, int Year), (BookingKind Kind, DateTime PaymentDate, DateTimeOffset CreatedAt, Guid Id, Action<BookingNumberEntity> Bind)> group
                 in pending.GroupBy(x => (x.Kind, x.PaymentDate.Year)))
        {
            int sequence = await _db.BookingNumbers
                .Where(x => x.Year == group.Key.Year && x.Kind == group.Key.Kind)
                .MaxAsync(x => (int?)x.Sequence) ?? 0;
            foreach ((BookingKind Kind, DateTime PaymentDate, DateTimeOffset CreatedAt, Guid Id, Action<BookingNumberEntity> Bind) item in group.OrderBy(x => x.PaymentDate).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id))
            {
                BookingNumberEntity number = new() { Id = Guid.NewGuid(), Year = group.Key.Year, Kind = group.Key.Kind, Sequence = ++sequence };
                item.Bind(number);
                await _db.BookingNumbers.AddAsync(number);
            }
        }

        await _db.SaveChangesAsync();
    }

    public async Task<Dictionary<Guid, string>> GetContributionNumbersAsync(IReadOnlyCollection<Guid> ids)
    {
        List<BookingNumberEntity> numbers = await _db.BookingNumbers
            .Where(x => x.ContributionId != null && ids.Contains(x.ContributionId.Value))
            .ToListAsync();
        return numbers.ToDictionary(x => x.ContributionId!.Value, x => x.Number);
    }

    public async Task<Dictionary<Guid, string>> GetIncomeNumbersAsync(IReadOnlyCollection<Guid> ids)
    {
        List<BookingNumberEntity> numbers = await _db.BookingNumbers
            .Where(x => x.IncomeId != null && ids.Contains(x.IncomeId.Value))
            .ToListAsync();
        return numbers.ToDictionary(x => x.IncomeId!.Value, x => x.Number);
    }

    public async Task<Dictionary<Guid, string>> GetReceiptNumbersAsync(IReadOnlyCollection<Guid> ids)
    {
        List<BookingNumberEntity> numbers = await _db.BookingNumbers
            .Where(x => x.ReceiptId != null && ids.Contains(x.ReceiptId.Value))
            .ToListAsync();
        return numbers.ToDictionary(x => x.ReceiptId!.Value, x => x.Number);
    }

    private async Task<BookingNumberEntity?> TryAssignAsync(BookingKind kind, int year,
        Expression<Func<BookingNumberEntity, bool>> owner, Action<BookingNumberEntity> bindOwner)
    {
        for (int attempt = 1; attempt <= MaxNumberingAttempts; attempt++)
        {
            BookingNumberEntity? existing = await _db.BookingNumbers.FirstOrDefaultAsync(owner);
            if (existing != null)
            {
                if (existing.Year != year)
                    return null;

                await _db.SaveChangesAsync();
                return existing;
            }

            int lastSequence = await _db.BookingNumbers
                .Where(x => x.Year == year && x.Kind == kind)
                .MaxAsync(x => (int?)x.Sequence) ?? 0;
            BookingNumberEntity draft = new() { Id = Guid.NewGuid(), Year = year, Kind = kind, Sequence = lastSequence + 1 };
            bindOwner(draft);

            try
            {
                await _db.BookingNumbers.AddAsync(draft);
                await _db.SaveChangesAsync();
                Log.Information("BookingNumberService: TryAssignAsync -> Booking number {Number} assigned.", draft.Number);
                return draft;
            }
            catch (DbUpdateException) when (attempt < MaxNumberingAttempts)
            {
                _db.Entry(draft).State = EntityState.Detached;
            }
        }

        throw new InvalidOperationException("Could not assign a booking number.");
    }
}
