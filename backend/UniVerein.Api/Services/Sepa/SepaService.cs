using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniVerein.Api.Data;
using UniVerein.Api.Data.Enums;
using UniVerein.Api.Data.Sepa;
using UniVerein.Api.Validators;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using Humanizer;
using Microsoft.EntityFrameworkCore;

namespace UniVerein.Api.Services.Sepa;

public class SepaService
{
    private readonly AppDbContext _db;
    private readonly CryptoService _crypto;
    private readonly SepaDdXmlBuilder _builder;

    public SepaService(AppDbContext db, CryptoService crypto)
    {
        _db = db;
        _crypto = crypto;
        _builder = new SepaDdXmlBuilder();
    }

    public async Task<(string xml, decimal amaunt, int count)> GenerateXml(CreditorConfig creditor, Guid exportId)
    {
        SepaValidator.ValidateCreditorConfig(creditor);

        List<DirectDebitTransaction> transactions = await GetTransactions(exportId);
        if (transactions.Count == 0)
            return ("", 0, 0);

        decimal amount = transactions.Sum(x => x.Amount);
        int sepaCount = transactions.Count;
        SepaDirectDebitDocument document = new()
        {
            GroupHeader = new GroupHeader
            {
                MessageId = $"UNI-VEREIN-{DateTime.UtcNow:yyyyMMddHHmmss}",
                CreationDateTime = DateTime.UtcNow,
                InitiatingParty = new Party { Name = creditor.Name },
            },
            PaymentInfos = new List<PaymentInfo>()
            {
                new()
                {
                    PaymentInfoId = "PMTINF-RCUR-001",
                    LocalInstrument = LocalInstrumentCode.CORE,
                    SequenceType = SequenceType.RCUR,
                    RequestedCollectionDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7)),
                    Creditor = new Party()
                    {
                        Name = creditor.Name,
                        PostalAddress = new Address()
                        {
                            City = $"{creditor.TownName}",
                            CountryCode = creditor.Country,
                            AddressLine = $"{creditor.StreetName}, {creditor.PostCode} {creditor.TownName}"
                        }
                    },
                    CreditorAccount = new BankAccount { IBAN = creditor.Iban, Currency = "EUR" },
                    CreditorAgent = new FinancialInstitution { BIC = creditor.Bic },
                    CreditorSchemeId = creditor.CreditorId,
                    Transactions = transactions
                }
            }
        };

        return (System.Text.Encoding.UTF8.GetString(Export(document)), amount, sepaCount);
    }

    private async Task<List<DirectDebitTransaction>> GetTransactions(Guid exportId)
    {
        List<ContributionEntity> contributions = await _db.Contributions
            .Include(c => c.MemberEntity)
            .ThenInclude(m => m.ContributionPlan)
            .Where(c =>
                c.Paid == null &&
                c.DueDate <= DateTime.Today &&
                c.MemberEntity != null &&
                c.MemberEntity.IBAN_Encrypted != null &&
                c.MemberEntity.Bic_Encrypted != null &&
                c.MemberEntity.SepaConsent != null &&
                c.ExportId == exportId)
            .ToListAsync();

        if (contributions.Count == 0)
            return [];

        List<DirectDebitTransaction> transactions = contributions.Select(x => new DirectDebitTransaction()
        {
            InstructionId = $"{x.MemberEntity.MemberNumber}-{DateTime.Today:yyyyMMdd}".Truncate(35),
            EndToEndId = $"E2E-{x.MemberEntity.MemberNumber}-{DateTime.Today:yyyyMMdd}".Truncate(35),
            Amount = x.Amount,
            Currency = "EUR",
            Mandate = new MandateInfo
            {
                MandateId = x.MemberEntity.MandateId.Replace("_", "-").Truncate(35),
                DateOfSignature = DateOnly.FromDateTime(((DateTimeOffset)x.MemberEntity.SepaConsent!).DateTime),
                AmendmentIndicator = false
            },
            Debtor = new Party
            {
                Name = $"{x.MemberEntity.FirstName} {x.MemberEntity.LastName}".Truncate(70),
                PostalAddress = new Address
                {
                    City = $"{x.MemberEntity.City}",
                    CountryCode = x.MemberEntity.CountryCode ?? string.Empty,
                    AddressLine =
                        $"{_crypto.Decrypt(x.MemberEntity.StreetEncrypted)}, {x.MemberEntity.PostalCode} {x.MemberEntity.City}"
                }
            },
            DebtorAccount = new BankAccount { IBAN = _crypto.Decrypt(x.MemberEntity.IBAN_Encrypted)?.Replace(" ", "") ?? string.Empty },
            DebtorAgent = new FinancialInstitution
                { BIC = _crypto.Decrypt(x.MemberEntity.Bic_Encrypted)?.Replace(" ", "") ?? string.Empty },
            RemittanceInfo =
                $"Membership fee {(x.MemberEntity.ContributionPlan?.Interval == Interval.MONTHLY ? $"{x.DueDate:yyyy-MM}" : $"{x.DueDate:yyyy}")}"
        }).ToList();

        return transactions.Where(x =>
            !string.IsNullOrWhiteSpace(x.DebtorAccount.IBAN) && !string.IsNullOrWhiteSpace(x.DebtorAgent.BIC)).ToList();
    }

    private byte[] Export(SepaDirectDebitDocument document)
    {
        int totalTxs = 0;
        decimal total = 0m;

        foreach (PaymentInfo pi in document.PaymentInfos)
        {
            pi.NumberOfTxs = pi.Transactions.Count;
            pi.ControlSum = pi.Transactions.Sum(t => t.Amount);
            totalTxs += pi.NumberOfTxs;
            total += pi.ControlSum;
        }

        document.GroupHeader.NumberOfTxs = totalTxs;
        document.GroupHeader.ControlSum = total;

        return _builder.Build(document);
    }
}