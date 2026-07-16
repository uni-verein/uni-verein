using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using UniVerein.Api.Models.Sepa;

namespace UniVerein.Api.Services.Sepa;

public class SepaDdXmlBuilder
{
    private static readonly XNamespace Namespace = "urn:iso:std:iso:20022:tech:xsd:pain.008.001.08";

    public byte[] Build(SepaDirectDebitDocument document)
    {
        XDocument xDoc = BuildXDocument(document);

        using MemoryStream ms = new();
        XmlWriterSettings settings = new()
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            Indent = true,
            IndentChars = "  ",
            NewLineOnAttributes = false
        };

        using (var writer = XmlWriter.Create(ms, settings))
            xDoc.Save(writer);

        return ms.ToArray();
    }

    private XDocument BuildXDocument(SepaDirectDebitDocument doc)
    {
        XElement root = new(Namespace + "Document",
            new XAttribute("xmlns", Namespace),
            new XElement(Namespace + "CstmrDrctDbtInitn",
                BuildGroupHeader(doc.GroupHeader),
                doc.PaymentInfos.Select(pi => BuildPaymentInfo(Namespace, pi))
            )
        );

        return new XDocument(new XDeclaration("1.0", "UTF-8", null), root);
    }

    private XElement BuildGroupHeader(GroupHeader hdr)
    {
        return new(Namespace + "GrpHdr",
            Elem("MsgId", Truncate(hdr.MessageId, 35)),
            Elem("CreDtTm", hdr.CreationDateTime.ToString("yyyy-MM-ddTHH:mm:ss")),
            Elem("NbOfTxs", hdr.NumberOfTxs.ToString()),
            Elem("CtrlSum", FormatAmount(hdr.ControlSum)),
            Elem("InitgPty", Elem("Nm", Truncate(hdr.InitiatingParty.Name, 140)))
        );
    }

    private XElement BuildPaymentInfo(XNamespace ns, PaymentInfo pi)
    {
        return new(ns + "PmtInf",
            Elem("PmtInfId", Truncate(pi.PaymentInfoId, 35)),
            Elem("PmtMtd", "DD"),
            Elem("BtchBookg", pi.BatchBooking ? "true" : "false"),
            Elem("NbOfTxs", pi.NumberOfTxs.ToString()),
            Elem("CtrlSum", FormatAmount(pi.ControlSum)),
            new XElement(ns + "PmtTpInf",
                Elem("SvcLvl", Elem("Cd", "SEPA")),
                Elem("LclInstrm", Elem("Cd", pi.LocalInstrument.ToString())),
                Elem("SeqTp", pi.SequenceType.ToString())
            ),
            Elem("ReqdColltnDt", pi.RequestedCollectionDate.ToString("yyyy-MM-dd")),
            BuildParty("Cdtr", pi.Creditor),
            BuildBankAccount("CdtrAcct", pi.CreditorAccount),
            BuildFinancialInstitution("CdtrAgt", pi.CreditorAgent),
            Elem("ChrgBr", "SLEV"),
            BuildCreditorSchemeId(ns, pi.CreditorSchemeId),
            pi.Transactions.Select(tx => BuildTransaction(ns, tx))
        );
    }

    private XElement BuildTransaction(XNamespace ns, DirectDebitTransaction tx)
    {
        return new(ns + "DrctDbtTxInf",
            new XElement(ns + "PmtId",
                Elem("InstrId", Truncate(tx.InstructionId, 35)),
                Elem("EndToEndId", Truncate(tx.EndToEndId, 35))
            ),
            new XElement(ns + "InstdAmt",
                new XAttribute("Ccy", tx.Currency),
                FormatAmount(tx.Amount)
            ),
            BuildDirectDebitTransaction(tx.Mandate),
            BuildFinancialInstitution("DbtrAgt", tx.DebtorAgent),
            BuildParty("Dbtr", tx.Debtor),
            BuildBankAccount("DbtrAcct", tx.DebtorAccount),
            tx.PurposeCode is { Length: > 0 } purp ? Elem("Purp", Elem("Cd", purp)) : null,
            Elem("RmtInf", Elem("Ustrd", Truncate(tx.RemittanceInfo, 140)))
        );
    }

    private XElement BuildDirectDebitTransaction(MandateInfo mandate)
    {
        return Elem("DrctDbtTx",
            Elem("MndtRltdInf", [
                    Elem("MndtId", Truncate(mandate.MandateId, 35)),
                    Elem("DtOfSgntr", mandate.DateOfSignature.ToString("yyyy-MM-dd")),
                    Elem("AmdmntInd", mandate.AmendmentIndicator ? "true" : "false")
                ]
            )
        );
    }

    private XElement BuildCreditorSchemeId(XNamespace ns, string creditorId)
    {
        return Elem("CdtrSchmeId",
            Elem("Id",
                Elem("PrvtId",
                    new XElement(ns + "Othr",
                        Elem("Id", creditorId),
                        Elem("SchmeNm", Elem("Prtry", "SEPA"))
                    )
                )
            )
        );
    }

    private XElement BuildParty(string tag, Party party)
    {
        return new(Namespace + tag,
            Elem("Nm", Truncate(party.Name, 140)),
            new XElement(Namespace + "PstlAdr", [
                    Elem("TwnNm", party.PostalAddress.City),
                    Elem("Ctry", string.IsNullOrWhiteSpace(party.PostalAddress.CountryCode) ? "DE" : party.PostalAddress.CountryCode),
                    party.PostalAddress.AddressLine is { Length: > 0 } adr
                        ? Elem("AdrLine", Truncate(adr, 70))
                        : null
                ]
            )
        );
    }

    private XElement BuildBankAccount(string tag, BankAccount account)
    {
        return Elem(tag, [
            Elem("Id", Elem("IBAN", NormalizeIban(account.IBAN)))
        ]);
    }

    private XElement BuildFinancialInstitution(string tag, FinancialInstitution fi)
    {
        return Elem(tag,
            Elem("FinInstnId",
                Elem("BICFI", string.IsNullOrWhiteSpace(fi.BIC) ? "NOTPROVIDED" : fi.BIC))
        );
    }

    private static XElement Elem(string name, string? value) =>
        new(Namespace + name, value ?? string.Empty);

    private static XElement Elem(string name, XElement value) =>
        new(Namespace + name, value);

    private static XElement Elem(string name, List<XElement> value) =>
        new(Namespace + name, value);

    private static string FormatAmount(decimal amount) =>
        amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static string NormalizeIban(string iban) =>
        iban.Replace(" ", string.Empty).ToUpperInvariant();
}