using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using UniVerein.Api.Models;
using UniVerein.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Serilog;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;

namespace UniVerein.Api.Services;

public class AcknowledgementService
{
    private const int MaxNumberingAttempts = 3;
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    private readonly AppDbContext _db;
    private readonly CryptoService _cryptoService;
    private readonly TimeProvider _timeProvider;
    private readonly ClubIdentityService _clubIdentityService;

    public AcknowledgementService(AppDbContext db, CryptoService cryptoService, TimeProvider timeProvider,
        ClubIdentityService clubIdentityService)
    {
        _db = db;
        _cryptoService = cryptoService;
        _timeProvider = timeProvider;
        _clubIdentityService = clubIdentityService;
    }

    public async Task<AcknowledgementIssueResult> IssueAsync(IncomeSource source, Guid id)
    {
        PaymentAcknowledgementEntity? existing = await FindAsync(source, id);
        if (existing != null)
            return new AcknowledgementIssueResult { Outcome = AcknowledgementOutcome.EXISTING, Acknowledgement = existing };

        (PaymentAcknowledgementEntity Draft, AcknowledgementOutcome Outcome)? snapshot = source == IncomeSource.CONTRIBUTION
            ? await SnapshotContributionAsync(id)
            : await SnapshotIncomeAsync(id);
        if (snapshot == null)
            return new AcknowledgementIssueResult { Outcome = AcknowledgementOutcome.NOT_FOUND };

        if (snapshot.Value.Outcome == AcknowledgementOutcome.NOT_PAID)
            return new AcknowledgementIssueResult { Outcome = AcknowledgementOutcome.NOT_PAID };

        PaymentAcknowledgementEntity draft = snapshot.Value.Draft;
        await ApplyIssuerAsync(draft);
        draft.Year = _timeProvider.GetUtcNow().Year;

        for (int attempt = 1; attempt <= MaxNumberingAttempts; attempt++)
        {
            int lastSequence = await _db.PaymentAcknowledgements
                .Where(x => x.Year == draft.Year)
                .MaxAsync(x => (int?)x.Sequence) ?? 0;
            draft.Id = Guid.NewGuid();
            draft.Sequence = lastSequence + 1;

            try
            {
                await _db.PaymentAcknowledgements.AddAsync(draft);
                await _db.SaveChangesAsync();
                Log.Information("AcknowledgementService: IssueAsync -> Acknowledgement {Number} issued.", draft.Number);
                return new AcknowledgementIssueResult { Outcome = AcknowledgementOutcome.ISSUED, Acknowledgement = draft };
            }
            catch (DbUpdateException) when (attempt < MaxNumberingAttempts)
            {
                _db.Entry(draft).State = EntityState.Detached;
                existing = await FindAsync(source, id);
                if (existing != null)
                    return new AcknowledgementIssueResult { Outcome = AcknowledgementOutcome.EXISTING, Acknowledgement = existing };
            }
        }

        throw new InvalidOperationException("Could not assign an acknowledgement number.");
    }

    private async Task<PaymentAcknowledgementEntity?> FindAsync(IncomeSource source, Guid id)
    {
        return source == IncomeSource.CONTRIBUTION
            ? await _db.PaymentAcknowledgements.FirstOrDefaultAsync(x => x.ContributionId == id)
            : await _db.PaymentAcknowledgements.FirstOrDefaultAsync(x => x.IncomeId == id);
    }

    private async Task<(PaymentAcknowledgementEntity, AcknowledgementOutcome)?> SnapshotContributionAsync(Guid id)
    {
        ContributionEntity? contribution = await _db.Contributions
            .Include(x => x.MemberEntity)
            .FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null);
        if (contribution == null)
            return null;

        MemberEntity member = contribution.MemberEntity;
        PaymentAcknowledgementEntity draft = new()
        {
            ContributionId = contribution.Id,
            PayerName = $"{member.FirstName} {member.LastName}",
            PayerAddress = ClubIdentityService.BuildAddress(_cryptoService.Decrypt(member.StreetEncrypted), member.PostalCode, member.City),
            Amount = contribution.Amount,
            Purpose = $"Mitgliedsbeitrag, fällig am {contribution.DueDate.ToString("dd.MM.yyyy", German)}",
            PaidAt = ToPaidAt(contribution.PaymentDate, contribution.Paid),
            IssuerName = string.Empty
        };

        return (draft, contribution.Paid == null ? AcknowledgementOutcome.NOT_PAID : AcknowledgementOutcome.ISSUED);
    }

    private async Task<(PaymentAcknowledgementEntity, AcknowledgementOutcome)?> SnapshotIncomeAsync(Guid id)
    {
        IncomeEntity? income = await _db.Incomes
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null);
        if (income == null)
            return null;

        string categoryName = income.Category?.Name ?? string.Empty;
        PaymentAcknowledgementEntity draft = new()
        {
            IncomeId = income.Id,
            PayerName = income.Payer,
            Amount = income.Amount,
            Purpose = string.IsNullOrWhiteSpace(income.Description)
                ? categoryName
                : $"{categoryName}: {income.Description}".TrimStart(':', ' '),
            PaidAt = ToPaidAt(income.PaymentDate, income.Paid),
            IssuerName = string.Empty
        };

        return (draft, income.Paid == null ? AcknowledgementOutcome.NOT_PAID : AcknowledgementOutcome.ISSUED);
    }

    private static DateTimeOffset ToPaidAt(DateTime? paymentDate, DateTimeOffset? paid)
    {
        if (paymentDate != null)
            return new DateTimeOffset(DateTime.SpecifyKind(paymentDate.Value.Date, DateTimeKind.Utc));

        return paid ?? default;
    }

    private async Task ApplyIssuerAsync(PaymentAcknowledgementEntity draft)
    {
        ClubIdentity club = await _clubIdentityService.GetAsync();
        draft.IssuerName = club.Name;
        draft.IssuerAddress = club.Address;
    }
}
