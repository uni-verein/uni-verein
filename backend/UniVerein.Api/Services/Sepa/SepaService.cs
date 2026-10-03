using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniVerein.Api.Models;
using UniVerein.Api.Models.Enums;
using UniVerein.Api.Models.Sepa;
using UniVerein.Api.Validators;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
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
        bool creditorPspOutsideEea = SepaCountries.IsNonEeaPsp(creditor.Iban, creditor.Bic);
        List<DirectDebitTransaction> transactions = await GetTransactions(exportId, creditorPspOutsideEea);
        if (transactions.Count == 0)
            return ("", 0, 0);

        decimal amount = transactions.Sum(x => x.Amount);
        int sepaCount = transactions.Count;
        DateTime now = DateTime.UtcNow;
        string messageId = $"UNI-VEREIN-{now:yyyyMMddHHmmss}";
        SepaDirectDebitDocument document = new()
        {
            GroupHeader = new GroupHeader
            {
                MessageId = messageId,
                CreationDateTime = now,
                InitiatingParty = new Party { Name = creditor.Name },
            },
            PaymentInfos = new List<PaymentInfo>()
            {
                new()
                {
                    PaymentInfoId = $"{messageId}-RCUR",
                    LocalInstrument = LocalInstrumentCode.CORE,
                    SequenceType = SequenceType.RCUR,
                    RequestedCollectionDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7)),
                    Creditor = new Party()
                    {
                        Name = creditor.Name,
                        PostalAddress = new Address()
                        {
                            StreetName = creditor.StreetName,
                            PostCode = creditor.PostCode,
                            City = creditor.TownName,
                            CountryCode = creditor.Country
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

    private async Task<List<DirectDebitTransaction>> GetTransactions(Guid exportId, bool creditorPspOutsideEea)
    {
        DateTime today = DateTime.UtcNow.Date;
        List<ContributionEntity> contributions = await _db.Contributions
            .Include(c => c.MemberEntity)
            .ThenInclude(m => m.ContributionPlan)
            .Where(c =>
                c.Paid == null &&
                c.DueDate <= today &&
                c.MemberEntity != null &&
                c.MemberEntity.IBAN_Encrypted != null &&
                c.MemberEntity.SepaConsent != null &&
                c.ExportId == exportId)
            .OrderBy(c => c.MemberEntity.MemberNumber)
            .ThenBy(c => c.DueDate)
            .ToListAsync();

        contributions = contributions.Where(c => c.Amount >= 0.01m).ToList();
        if (contributions.Count == 0)
            return [];

        List<DirectDebitTransaction> transactions = [];
        foreach (ContributionEntity x in contributions)
        {
            string iban = SepaText.Iban(_crypto.Decrypt(x.MemberEntity.IBAN_Encrypted));
            if (!SepaValidator.IsValidIban(iban))
                continue;

            string bic = _crypto.Decrypt(x.MemberEntity.Bic_Encrypted) ?? string.Empty;
            bool pspOutsideEea = creditorPspOutsideEea || SepaCountries.IsNonEeaPsp(iban, bic);
            Address address = new()
            {
                StreetName = _crypto.Decrypt(x.MemberEntity.StreetEncrypted),
                PostCode = x.MemberEntity.PostalCode,
                City = x.MemberEntity.City,
                CountryCode = x.MemberEntity.CountryCode ?? string.Empty
            };

            if (pspOutsideEea && !HasTownAndCountry(address))
                continue;

            transactions.Add(new DirectDebitTransaction()
            {
                // Unique per contribution, so several open contributions of one member never collide.
                InstructionId = x.Id.ToString("N"),
                EndToEndId = $"M{x.MemberEntity.MemberNumber}-{x.DueDate:yyyyMMdd}-{x.Id.ToString("N")[..8]}",
                Amount = x.Amount,
                Currency = "EUR",
                Mandate = new MandateInfo
                {
                    MandateId = x.MemberEntity.MandateId,
                    DateOfSignature = DateOnly.FromDateTime(((DateTimeOffset)x.MemberEntity.SepaConsent!).DateTime),
                    AmendmentIndicator = false
                },
                Debtor = new Party
                {
                    Name = $"{x.MemberEntity.FirstName} {x.MemberEntity.LastName}",
                    PostalAddress = address
                },
                DebtorAccount = new BankAccount { IBAN = iban },
                DebtorAgent = new FinancialInstitution { BIC = bic },
                RemittanceInfo =
                    $"Membership fee {(x.MemberEntity.ContributionPlan?.Interval == Interval.MONTHLY ? $"{x.DueDate:yyyy-MM}" : $"{x.DueDate:yyyy}")}"
            });
        }

        return transactions;
    }

    private static bool HasTownAndCountry(Address address)
    {
        return SepaText.Text(address.City, 35).Length > 0 && SepaText.CountryCode(address.CountryCode) != null;
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