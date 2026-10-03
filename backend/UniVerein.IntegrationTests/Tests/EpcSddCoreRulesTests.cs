using System.Xml.Linq;
using System.Xml.Schema;
using UniVerein.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class EpcSddCoreRulesTests
{
    private static readonly XNamespace Ns = "urn:iso:std:iso:20022:tech:xsd:pain.008.001.08";

    private const string ValidDocument = """
        <?xml version="1.0" encoding="utf-8"?>
        <Document xmlns="urn:iso:std:iso:20022:tech:xsd:pain.008.001.08">
          <CstmrDrctDbtInitn>
            <GrpHdr>
              <MsgId>MSG-1</MsgId>
              <CreDtTm>2026-11-16T10:00:00Z</CreDtTm>
              <NbOfTxs>1</NbOfTxs>
              <CtrlSum>12.50</CtrlSum>
              <InitgPty><Nm>Musterverein e.V.</Nm></InitgPty>
            </GrpHdr>
            <PmtInf>
              <PmtInfId>MSG-1-RCUR</PmtInfId>
              <PmtMtd>DD</PmtMtd>
              <BtchBookg>false</BtchBookg>
              <NbOfTxs>1</NbOfTxs>
              <CtrlSum>12.50</CtrlSum>
              <PmtTpInf>
                <SvcLvl><Cd>SEPA</Cd></SvcLvl>
                <LclInstrm><Cd>CORE</Cd></LclInstrm>
                <SeqTp>RCUR</SeqTp>
              </PmtTpInf>
              <ReqdColltnDt>2026-11-20</ReqdColltnDt>
              <Cdtr>
                <Nm>Musterverein e.V.</Nm>
                <PstlAdr>
                  <StrtNm>Vereinsstrasse 1</StrtNm>
                  <PstCd>12345</PstCd>
                  <TwnNm>Musterstadt</TwnNm>
                  <Ctry>DE</Ctry>
                </PstlAdr>
              </Cdtr>
              <CdtrAcct><Id><IBAN>DE89370400440532013000</IBAN></Id></CdtrAcct>
              <CdtrAgt><FinInstnId><BICFI>COBADEFFXXX</BICFI></FinInstnId></CdtrAgt>
              <ChrgBr>SLEV</ChrgBr>
              <CdtrSchmeId>
                <Id><PrvtId><Othr><Id>DE98ZZZ09999999999</Id><SchmeNm><Prtry>SEPA</Prtry></SchmeNm></Othr></PrvtId></Id>
              </CdtrSchmeId>
              <DrctDbtTxInf>
                <PmtId>
                  <InstrId>0123456789abcdef0123456789abcdef</InstrId>
                  <EndToEndId>M1-20261101-01234567</EndToEndId>
                </PmtId>
                <InstdAmt Ccy="EUR">12.50</InstdAmt>
                <DrctDbtTx>
                  <MndtRltdInf>
                    <MndtId>20240101120000-1</MndtId>
                    <DtOfSgntr>2024-01-01</DtOfSgntr>
                    <AmdmntInd>false</AmdmntInd>
                  </MndtRltdInf>
                </DrctDbtTx>
                <DbtrAgt><FinInstnId><Othr><Id>NOTPROVIDED</Id></Othr></FinInstnId></DbtrAgt>
                <Dbtr>
                  <Nm>Max Mustermann</Nm>
                  <PstlAdr>
                    <PstCd>12345</PstCd>
                    <TwnNm>Musterstadt</TwnNm>
                    <Ctry>DE</Ctry>
                    <AdrLine>Musterstrasse 1</AdrLine>
                  </PstlAdr>
                </Dbtr>
                <DbtrAcct><Id><IBAN>DE02120300000000202051</IBAN></Id></DbtrAcct>
                <RmtInf><Ustrd>Membership fee 2026-11</Ustrd></RmtInf>
              </DrctDbtTxInf>
            </PmtInf>
          </CstmrDrctDbtInitn>
        </Document>
        """;

    [Fact]
    public void ValidDocument_HasNoErrors()
    {
        EpcSddCoreRules.Validate(XDocument.Parse(ValidDocument)).ShouldBeEmpty();
    }

    [Fact]
    public void ValidDocument_IsValidAgainstIsoSchema()
    {
        // Arrange
        XmlSchemaSet schemas = new();
        schemas.Add(Ns.NamespaceName, Path.Combine(AppContext.BaseDirectory, "Resources", "Sepa", "pain.008.001.08.xsd"));
        List<string> errors = [];

        // Act
        XDocument.Parse(ValidDocument).Validate(schemas, (_, e) => errors.Add(e.Message));

        // Assert
        errors.ShouldBeEmpty();
    }

    public static TheoryData<string, string> Violations => new()
    {
        // Elements ISO allows but SEPA does not
        { "GrpHdr/InitgPty|<PstlAdr><TwnNm>Musterstadt</TwnNm><Ctry>DE</Ctry></PstlAdr>", "InitgPty/PstlAdr: element is not allowed" },
        { "PmtInf/PmtTpInf|<InstrPrty>HIGH</InstrPrty>", "PmtTpInf/InstrPrty: element is not allowed" },
        { "PmtInf/Cdtr/PstlAdr|<AdrTp><Cd>HOME</Cd></AdrTp>", "Cdtr/PstlAdr/AdrTp: element is not allowed" },
        { "PmtInf/CdtrAgt/FinInstnId|<LEI>529900T8BM49AURSDO55</LEI>", "FinInstnId/LEI: element is not allowed" },
        { "PmtInf/DrctDbtTxInf/PmtId|<UETR>eb6305c9-1f7f-49de-aed0-16487c27b42d</UETR>", "PmtId/UETR: element is not allowed" },
        { "PmtInf/DrctDbtTxInf|<Tax />", "DrctDbtTxInf/Tax: element is not allowed" },
        { "PmtInf/DrctDbtTxInf/DrctDbtTx/MndtRltdInf|<Frqcy />", "MndtRltdInf/Frqcy: element is not allowed" },
        { "PmtInf/CdtrSchmeId|<Nm>Musterverein</Nm>", "CdtrSchmeId/Nm: element is not allowed" },
        // Codes
        { "PmtInf/PmtTpInf/SvcLvl/Cd=NURG", "'NURG' is not allowed, expected SEPA" },
        { "PmtInf/PmtTpInf/LclInstrm/Cd=B2B", "'B2B' is not allowed, expected CORE" },
        { "PmtInf/PmtTpInf/SeqTp=RPRE", "'RPRE' is not allowed" },
        { "PmtInf/ChrgBr=SHAR", "'SHAR' is not allowed, expected SLEV" },
        { "PmtInf/DrctDbtTxInf/DbtrAgt/FinInstnId/Othr/Id=UNKNOWN", "only 'NOTPROVIDED' is allowed" },
        { "PmtInf/CdtrSchmeId/Id/PrvtId/Othr/SchmeNm/Prtry=CORE", "'SchmeNm/Prtry' must be 'SEPA'" },
        // Amounts
        { "PmtInf/DrctDbtTxInf/InstdAmt=0.00", "must be between 0.01 and 999999999.99" },
        { "PmtInf/DrctDbtTxInf/InstdAmt=1000000000.00", "must be between 0.01 and 999999999.99" },
        { "PmtInf/DrctDbtTxInf/InstdAmt=12.505", "must be between 0.01 and 999999999.99" },
        { "PmtInf/DrctDbtTxInf/InstdAmt@Ccy=CHF", "only 'EUR' is allowed" },
        { "GrpHdr/CtrlSum=12.505", "must have at most two decimals" },
        // Lengths and character set
        { "PmtInf/Cdtr/Nm=" + new string('A', 71), "name must be 1-70 characters" },
        { "PmtInf/DrctDbtTxInf/RmtInf/Ustrd=" + new string('A', 141), "must not exceed 140 characters" },
        { "PmtInf/DrctDbtTxInf/Dbtr/Nm=Max Müller", "outside the Latin character set" },
        { "PmtInf/DrctDbtTxInf/RmtInf/Ustrd=Beitrag & Spende", "outside the Latin character set" },
        // Identifiers
        { "PmtInf/DrctDbtTxInf/DrctDbtTx/MndtRltdInf/MndtId=/MANDATE-1", "must not start or end with '/'" },
        { "PmtInf/DrctDbtTxInf/PmtId/EndToEndId=M1//2026", "must not start or end with '/' or contain '//'" },
        // Addresses (November 2026)
        { "PmtInf/DrctDbtTxInf/Dbtr/PstlAdr/TwnNm-", "'TwnNm' and 'Ctry' are mandatory" },
        { "PmtInf/Cdtr/PstlAdr/Ctry-", "'TwnNm' and 'Ctry' are mandatory" },
        { "PmtInf/DrctDbtTxInf/Dbtr/PstlAdr|<AdrLine>A</AdrLine><AdrLine>B</AdrLine>", "at most two 'AdrLine'" },
        // Mandatory elements
        { "PmtInf/DrctDbtTxInf/DrctDbtTx/MndtRltdInf/DtOfSgntr-", "DtOfSgntr is missing" },
        { "PmtInf/CdtrAgt/FinInstnId/BICFI-", "either 'BICFI' or 'Othr/Id' must be used" },
        { "PmtInf/CdtrSchmeId-", "'CdtrSchmeId' must be present" },
        { "PmtInf/PmtTpInf/SeqTp-", "SeqTp is missing" },
        // Remittance information
        { "PmtInf/DrctDbtTxInf/RmtInf|<Ustrd>Second line</Ustrd>", "only one occurrence of 'Ustrd' or 'Strd'" },
    };

    [Theory]
    [MemberData(nameof(Violations))]
    public void Violation_IsDetected(string mutation, string expectedError)
    {
        // Arrange
        XDocument document = Mutate(mutation);

        // Act
        List<string> errors = EpcSddCoreRules.Validate(document);

        // Assert
        errors.ShouldContain(x => x.Contains(expectedError), $"Errors: {string.Join("; ", errors)}");
    }

    internal static XDocument Mutate(string mutation)
    {
        XDocument document = XDocument.Parse(ValidDocument);

        if (mutation.Contains('|'))
        {
            string[] parts = mutation.Split('|', 2);
            XElement fragment = XElement.Parse($"<Wrapper xmlns=\"{Ns.NamespaceName}\">{parts[1]}</Wrapper>");
            Find(document, parts[0]).Add(fragment.Elements());
        }
        else if (mutation.EndsWith('-'))
        {
            Find(document, mutation[..^1]).Remove();
        }
        else if (mutation.Contains('@'))
        {
            string[] parts = mutation.Split('@', 2);
            string[] attribute = parts[1].Split('=', 2);
            Find(document, parts[0]).SetAttributeValue(attribute[0], attribute[1]);
        }
        else
        {
            string[] parts = mutation.Split('=', 2);
            Find(document, parts[0]).Value = parts[1];
        }

        return document;
    }

    private static XElement Find(XDocument document, string path)
    {
        XElement current = document.Root!.Element(Ns + "CstmrDrctDbtInitn")!;
        foreach (string segment in path.Split('/'))
            current = current.Element(Ns + segment) ?? throw new InvalidOperationException($"'{segment}' not found in {path}");

        return current;
    }
}
