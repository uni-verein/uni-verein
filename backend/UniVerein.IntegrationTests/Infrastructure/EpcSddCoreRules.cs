using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace UniVerein.IntegrationTests.Infrastructure;

public static class EpcSddCoreRules
{
    private static readonly XNamespace Ns = "urn:iso:std:iso:20022:tech:xsd:pain.008.001.08";
    private static readonly Regex LatinCharacterSet = new(@"^[a-zA-Z0-9/\-?:().,'+ ]*$");
    private static readonly string[] Identifiers = ["MsgId", "PmtInfId", "InstrId", "EndToEndId", "MndtId"];
    private static readonly string[] SequenceTypes = ["FRST", "RCUR", "FNAL", "OOFF"];

    // Elements that ISO 20022 allows but SEPA does not, keyed by the path of the parent element.
    // A path segment "*" matches any element name.
    private static readonly (string Parent, string[] Children)[] ForbiddenElements =
    [
        ("CstmrDrctDbtInitn", ["SplmtryData"]),
        ("GrpHdr", ["Authstn", "FwdgAgt"]),
        ("GrpHdr/InitgPty", ["PstlAdr", "CtryOfRes", "CtctDtls"]),
        ("PmtInf", ["CdtrAgtAcct", "ChrgsAcct", "ChrgsAcctAgt"]),
        ("PmtTpInf", ["InstrPrty"]),
        ("PmtTpInf/SvcLvl", ["Prtry"]),
        ("PmtTpInf/LclInstrm", ["Prtry"]),
        ("PmtInf/Cdtr", ["Id", "CtryOfRes", "CtctDtls"]),
        ("Cdtr/PstlAdr", ["AdrTp"]),
        ("Dbtr/PstlAdr", ["AdrTp"]),
        ("CdtrAcct", ["Tp", "Nm", "Prxy"]),
        ("DbtrAcct", ["Tp", "Ccy", "Nm", "Prxy"]),
        ("CdtrAcct/Id", ["Othr"]),
        ("DbtrAcct/Id", ["Othr"]),
        ("CdtrAgt", ["BrnchId"]),
        ("DbtrAgt", ["BrnchId"]),
        ("FinInstnId", ["ClrSysMmbId", "LEI", "Nm", "PstlAdr"]),
        ("FinInstnId/Othr", ["SchmeNm", "Issr"]),
        ("UltmtCdtr", ["PstlAdr", "CtryOfRes", "CtctDtls"]),
        ("UltmtDbtr", ["PstlAdr", "CtryOfRes", "CtctDtls"]),
        ("CdtrSchmeId", ["Nm", "PstlAdr", "CtryOfRes", "CtctDtls"]),
        ("CdtrSchmeId/Id", ["OrgId"]),
        ("CdtrSchmeId/Id/PrvtId", ["DtAndPlcOfBirth"]),
        ("CdtrSchmeId/Id/PrvtId/Othr", ["Issr"]),
        ("CdtrSchmeId/Id/PrvtId/Othr/SchmeNm", ["Cd"]),
        ("DrctDbtTxInf", ["DbtrAgtAcct", "InstrForCdtrAgt", "RgltryRptg", "Tax", "RltdRmtInf", "SplmtryData"]),
        ("DrctDbtTxInf/PmtId", ["UETR"]),
        ("DrctDbtTx", ["PreNtfctnId", "PreNtfctnDt"]),
        ("MndtRltdInf", ["FrstColltnDt", "FnlColltnDt", "Frqcy", "Rsn", "TrckgDays"]),
        ("DrctDbtTxInf/Dbtr", ["CtryOfRes", "CtctDtls"]),
        ("Purp", ["Prtry"]),
        ("RmtInf/Strd", ["RfrdDocInf", "RfrdDocAmt", "Invcr", "Invcee", "TaxRmt", "GrnshmtRmt", "AddtlRmtInf"]),
    ];

    public static List<string> Validate(XDocument document)
    {
        List<string> errors = [];
        XElement? initiation = document.Root?.Element(Ns + "CstmrDrctDbtInitn");
        if (document.Root?.Name != Ns + "Document" || initiation == null)
            return ["Document/CstmrDrctDbtInitn (pain.008.001.08) is missing"];

        CheckForbiddenElements(document, errors);
        CheckCharacterSet(document, errors);
        CheckIdentifiers(document, errors);
        CheckNames(document, errors);
        CheckAddresses(document, errors);
        CheckGroupHeader(initiation, errors);
        foreach (XElement paymentInfo in initiation.Elements(Ns + "PmtInf"))
            CheckPaymentInformation(paymentInfo, errors);

        return errors;
    }

    private static void CheckForbiddenElements(XDocument document, List<string> errors)
    {
        foreach (XElement element in document.Descendants())
        {
            foreach ((string parent, string[] children) in ForbiddenElements)
            {
                if (children.Contains(element.Name.LocalName) && element.Parent != null && PathEndsWith(element.Parent, parent))
                    errors.Add($"{PathOf(element)}: element is not allowed in SEPA");
            }
        }
    }

    private static void CheckCharacterSet(XDocument document, List<string> errors)
    {
        foreach (XElement element in document.Descendants().Where(x => !x.HasElements))
        {
            if (!LatinCharacterSet.IsMatch(element.Value))
                errors.Add($"{PathOf(element)}: '{element.Value}' contains characters outside the Latin character set");
        }
    }

    private static void CheckIdentifiers(XDocument document, List<string> errors)
    {
        IEnumerable<XElement> identifiers = document.Descendants()
            .Where(x => Identifiers.Contains(x.Name.LocalName))
            .Concat(document.Descendants(Ns + "CdtrSchmeId").Descendants(Ns + "Othr").Elements(Ns + "Id"));

        foreach (XElement id in identifiers)
        {
            if (id.Value.Length is < 1 or > 35)
                errors.Add($"{PathOf(id)}: must be 1-35 characters");
            if (id.Value.StartsWith('/') || id.Value.EndsWith('/') || id.Value.Contains("//"))
                errors.Add($"{PathOf(id)}: must not start or end with '/' or contain '//'");
        }
    }

    private static void CheckNames(XDocument document, List<string> errors)
    {
        foreach (XElement name in document.Descendants(Ns + "Nm"))
        {
            if (name.Value.Length is < 1 or > 70)
                errors.Add($"{PathOf(name)}: name must be 1-70 characters");
        }
    }

    // EPC153-22: as of 15 November 2026 only structured and hybrid addresses are allowed. Both need
    // 'Town Name' and 'Country', a hybrid address may add at most two 'Address Line' elements.
    private static void CheckAddresses(XDocument document, List<string> errors)
    {
        foreach (XElement address in document.Descendants(Ns + "PstlAdr"))
        {
            if (address.Element(Ns + "TwnNm") == null || address.Element(Ns + "Ctry") == null)
                errors.Add($"{PathOf(address)}: 'TwnNm' and 'Ctry' are mandatory (structured or hybrid address)");

            List<XElement> lines = address.Elements(Ns + "AdrLine").ToList();
            if (lines.Count > 2)
                errors.Add($"{PathOf(address)}: at most two 'AdrLine' elements are allowed");
            if (lines.Any(x => x.Value.Length > 70))
                errors.Add($"{PathOf(address)}: 'AdrLine' must not exceed 70 characters");
        }
    }

    private static void CheckGroupHeader(XElement initiation, List<string> errors)
    {
        XElement? header = initiation.Element(Ns + "GrpHdr");
        if (header == null)
        {
            errors.Add("GrpHdr is missing");
            return;
        }

        CheckDecimal(header, "CtrlSum", errors);
    }

    private static void CheckPaymentInformation(XElement paymentInfo, List<string> errors)
    {
        Require(paymentInfo, "NbOfTxs", errors);
        CheckDecimal(paymentInfo, "CtrlSum", errors);
        CheckPaymentType(paymentInfo.Element(Ns + "PmtTpInf"), PathOf(paymentInfo) + "/PmtTpInf", required: true, errors);
        CheckCode(paymentInfo, "ChrgBr", ["SLEV"], errors);
        Require(paymentInfo.Element(Ns + "Cdtr"), "Nm", errors, PathOf(paymentInfo) + "/Cdtr");
        CheckAgent(paymentInfo.Element(Ns + "CdtrAgt"), PathOf(paymentInfo) + "/CdtrAgt", errors);

        XElement? schemeIdAtPaymentInfo = paymentInfo.Element(Ns + "CdtrSchmeId");
        if (schemeIdAtPaymentInfo != null)
            CheckCreditorSchemeId(schemeIdAtPaymentInfo, errors);

        foreach (XElement transaction in paymentInfo.Elements(Ns + "DrctDbtTxInf"))
            CheckTransaction(transaction, schemeIdAtPaymentInfo != null, errors);
    }

    private static void CheckTransaction(XElement transaction, bool hasSchemeIdAtPaymentInfo, List<string> errors)
    {
        string path = PathOf(transaction);
        CheckPaymentType(transaction.Element(Ns + "PmtTpInf"), path + "/PmtTpInf", required: false, errors);
        CheckCode(transaction, "ChrgBr", ["SLEV"], errors);

        XElement? amount = transaction.Element(Ns + "InstdAmt");
        if (amount == null)
        {
            errors.Add($"{path}/InstdAmt is missing");
        }
        else
        {
            if (amount.Attribute("Ccy")?.Value != "EUR")
                errors.Add($"{path}/InstdAmt: only 'EUR' is allowed");
            if (!decimal.TryParse(amount.Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal value)
                || value < 0.01m || value > 999999999.99m || decimal.Round(value, 2) != value)
                errors.Add($"{path}/InstdAmt: '{amount.Value}' must be between 0.01 and 999999999.99 with at most two decimals");
        }

        XElement? mandate = transaction.Element(Ns + "DrctDbtTx")?.Element(Ns + "MndtRltdInf");
        if (mandate == null)
        {
            errors.Add($"{path}/DrctDbtTx/MndtRltdInf is missing");
        }
        else
        {
            Require(mandate, "MndtId", errors);
            Require(mandate, "DtOfSgntr", errors);
        }

        XElement? schemeIdAtTransaction = transaction.Element(Ns + "DrctDbtTx")?.Element(Ns + "CdtrSchmeId");
        if (schemeIdAtTransaction != null)
        {
            CheckCreditorSchemeId(schemeIdAtTransaction, errors);
        }
        else if (!hasSchemeIdAtPaymentInfo)
        {
            errors.Add($"{path}: 'CdtrSchmeId' must be present at payment information or transaction level");
        }

        CheckAgent(transaction.Element(Ns + "DbtrAgt"), path + "/DbtrAgt", errors);
        Require(transaction.Element(Ns + "Dbtr"), "Nm", errors, path + "/Dbtr");

        XElement? remittance = transaction.Element(Ns + "RmtInf");
        if (remittance != null)
        {
            List<XElement> unstructured = remittance.Elements(Ns + "Ustrd").ToList();
            if (unstructured.Count > 1 || remittance.Elements(Ns + "Strd").Count() > 1)
                errors.Add($"{path}/RmtInf: only one occurrence of 'Ustrd' or 'Strd' is allowed");
            if (unstructured.Any(x => x.Value.Length > 140))
                errors.Add($"{path}/RmtInf/Ustrd: must not exceed 140 characters");
        }
    }

    private static void CheckPaymentType(XElement? paymentType, string path, bool required, List<string> errors)
    {
        if (paymentType == null)
        {
            if (required)
                errors.Add($"{path} is missing");
            return;
        }

        if (paymentType.Elements(Ns + "SvcLvl").Count() > 1)
            errors.Add($"{path}: only one 'SvcLvl' is allowed");
        CheckCode(paymentType.Element(Ns + "SvcLvl"), "Cd", ["SEPA"], errors, path + "/SvcLvl", required);
        CheckCode(paymentType.Element(Ns + "LclInstrm"), "Cd", ["CORE"], errors, path + "/LclInstrm", required);
        CheckCode(paymentType, "SeqTp", SequenceTypes, errors, path, required);
    }

    private static void CheckAgent(XElement? agent, string path, List<string> errors)
    {
        XElement? institution = agent?.Element(Ns + "FinInstnId");
        if (institution == null)
        {
            errors.Add($"{path}/FinInstnId is missing");
            return;
        }

        XElement? other = institution.Element(Ns + "Othr");
        if (institution.Element(Ns + "BICFI") == null && other == null)
            errors.Add($"{path}/FinInstnId: either 'BICFI' or 'Othr/Id' must be used");
        if (other != null && other.Element(Ns + "Id")?.Value != "NOTPROVIDED")
            errors.Add($"{path}/FinInstnId/Othr/Id: only 'NOTPROVIDED' is allowed");
    }

    private static void CheckCreditorSchemeId(XElement schemeId, List<string> errors)
    {
        List<XElement> others = schemeId.Element(Ns + "Id")?.Element(Ns + "PrvtId")?.Elements(Ns + "Othr").ToList() ?? [];
        if (others.Count != 1)
        {
            errors.Add($"{PathOf(schemeId)}: exactly one 'Id/PrvtId/Othr' is required");
            return;
        }

        if (others[0].Element(Ns + "SchmeNm")?.Element(Ns + "Prtry")?.Value != "SEPA")
            errors.Add($"{PathOf(schemeId)}: 'SchmeNm/Prtry' must be 'SEPA'");
    }

    private static void CheckDecimal(XElement parent, string name, List<string> errors)
    {
        XElement? element = parent.Element(Ns + name);
        if (element == null)
        {
            errors.Add($"{PathOf(parent)}/{name} is missing");
        }
        else if (!Regex.IsMatch(element.Value, @"^\d{1,16}(\.\d{1,2})?$"))
        {
            errors.Add($"{PathOf(element)}: '{element.Value}' must have at most two decimals");
        }
    }

    private static void CheckCode(XElement? parent, string name, string[] allowed, List<string> errors,
        string? path = null, bool required = false)
    {
        XElement? element = parent?.Element(Ns + name);
        if (element == null)
        {
            if (required)
                errors.Add($"{path ?? PathOf(parent!)}/{name} is missing");
            return;
        }

        if (!allowed.Contains(element.Value))
            errors.Add($"{PathOf(element)}: '{element.Value}' is not allowed, expected {string.Join(" or ", allowed)}");
    }

    private static void Require(XElement? parent, string name, List<string> errors, string? path = null)
    {
        if (parent?.Element(Ns + name) == null)
            errors.Add($"{path ?? (parent == null ? "?" : PathOf(parent))}/{name} is missing");
    }

    private static bool PathEndsWith(XElement element, string path)
    {
        XElement? current = element;
        foreach (string segment in path.Split('/').Reverse())
        {
            if (current == null || (segment != "*" && current.Name.LocalName != segment))
                return false;
            current = current.Parent;
        }

        return true;
    }

    private static string PathOf(XElement element)
    {
        return string.Join("/", element.AncestorsAndSelf().Reverse().Select(x => x.Name.LocalName));
    }
}
