using System.Collections.Generic;
using System.Globalization;
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
    private const int MaxNameLength = 70;

    public byte[] Build(SepaDirectDebitDocument document)
    {
        XDocument xDoc = BuildXDocument(document);

        using MemoryStream ms = new();
        XmlWriterSettings settings = new()
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
            IndentChars = "  ",
            NewLineOnAttributes = false
        };

        using (XmlWriter writer = XmlWriter.Create(ms, settings))
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
            Elem("MsgId", SepaText.Id(hdr.MessageId)),
            Elem("CreDtTm", hdr.CreationDateTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)),
            Elem("NbOfTxs", hdr.NumberOfTxs.ToString()),
            Elem("CtrlSum", FormatAmount(hdr.ControlSum)),
            Elem("InitgPty", Elem("Nm", SepaText.Text(hdr.InitiatingParty.Name, MaxNameLength)))
        );
    }

    private XElement BuildPaymentInfo(XNamespace ns, PaymentInfo pi)
    {
        return new(ns + "PmtInf",
            Elem("PmtInfId", SepaText.Id(pi.PaymentInfoId)),
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
                Elem("InstrId", SepaText.Id(tx.InstructionId)),
                Elem("EndToEndId", SepaText.Id(tx.EndToEndId))
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
            Elem("RmtInf", Elem("Ustrd", SepaText.Text(tx.RemittanceInfo, 140)))
        );
    }

    private XElement BuildDirectDebitTransaction(MandateInfo mandate)
    {
        return Elem("DrctDbtTx",
            Elem("MndtRltdInf", [
                    Elem("MndtId", SepaText.Id(mandate.MandateId)),
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
                        Elem("Id", SepaText.CreditorId(creditorId)),
                        Elem("SchmeNm", Elem("Prtry", "SEPA"))
                    )
                )
            )
        );
    }

    private XElement BuildParty(string tag, Party party)
    {
        return new(Namespace + tag,
            Elem("Nm", SepaText.Text(party.Name, MaxNameLength)),
            BuildPostalAddress(party.PostalAddress)
        );
    }

    // Fully structured address (no AdrLine) as required from November 2026.
    // Town and country are mandatory as soon as an address is given otherwise the address is omitted,
    // which is allowed for debtors within the EU/EEA.
    private XElement? BuildPostalAddress(Address? address)
    {
        if (address == null)
            return null;

        string town = SepaText.Text(address.City, 35);
        string? country = SepaText.CountryCode(address.CountryCode);
        if (town.Length == 0 || country == null)
            return null;

        string street = SepaText.Text(address.StreetName, 70);
        string postCode = SepaText.Text(address.PostCode, 16);

        return new XElement(Namespace + "PstlAdr",
            street.Length > 0 ? Elem("StrtNm", street) : null,
            postCode.Length > 0 ? Elem("PstCd", postCode) : null,
            Elem("TwnNm", town),
            Elem("Ctry", country)
        );
    }

    private XElement BuildBankAccount(string tag, BankAccount account)
    {
        return Elem(tag, [
            Elem("Id", Elem("IBAN", SepaText.Iban(account.IBAN)))
        ]);
    }

    // Without a (valid) BIC the agent is identified as NOTPROVIDED (IBAN-only), the only allowed value for
    // 'Other/Identification'.
    private XElement BuildFinancialInstitution(string tag, FinancialInstitution fi)
    {
        string? bic = SepaText.Bic(fi.BIC);
        return Elem(tag,
            Elem("FinInstnId", bic != null
                ? Elem("BICFI", bic)
                : Elem("Othr", Elem("Id", "NOTPROVIDED"))));
    }

    private static XElement Elem(string name, string? value) =>
        new(Namespace + name, value ?? string.Empty);

    private static XElement Elem(string name, XElement value) =>
        new(Namespace + name, value);

    private static XElement Elem(string name, List<XElement> value) =>
        new(Namespace + name, value);

    private static string FormatAmount(decimal amount) =>
        amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
}