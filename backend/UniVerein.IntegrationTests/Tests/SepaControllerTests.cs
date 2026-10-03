using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Xml.Linq;
using System.Xml.Schema;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Services;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class SepaControllerTests : IntegrationTestBase
{
    private static readonly XNamespace Ns = "urn:iso:std:iso:20022:tech:xsd:pain.008.001.08";

    private const string CreditorIban = "DE89370400440532013000";
    private const string CreditorBic = "COBADEFFXXX";
    private const string MemberIban = "DE02120300000000202051";
    private const string MemberBic = "BYLADEM1001";
    private const string SwissIban = "CH9300762011623852957";
    private const string SwissBic = "UBSWCHZH80A";
    private const string IsoSchema = "pain.008.001.08.xsd";

    private readonly CryptoService _crypto;

    public SepaControllerTests(UniVereinWebApplicationFactory factory) : base(factory)
    {
        _crypto = GetService<CryptoService>();
    }

    public override async Task InitializeAsync()
    {
        await ClearSepaDataAsync();
    }

    public override async Task DisposeAsync()
    {
        await ClearSepaDataAsync();
        await base.DisposeAsync();
    }

    // ---------------------------------------------------------------
    // GET /sepa/export/{id} - authorization
    // ---------------------------------------------------------------

    [Fact]
    public async Task Export_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync($"/sepa/export/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Export_AsUser_ReturnsForbidden()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        HttpResponseMessage response = await client.GetAsync($"/sepa/export/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task Export_AsAuthorizedRole_ReturnsXmlFile(UserRole role)
    {
        // Arrange
        await CreateCreditorConfigAsync();
        Guid exportId = Guid.NewGuid();
        MemberEntity member = await CreateMemberAsync(1);
        await CreateContributionAsync(member, exportId);

        // Act
        HttpResponseMessage response = await CreateClient(role).GetAsync($"/sepa/export/{exportId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/xml");
        response.Content.Headers.ContentDisposition?.FileName.ShouldBe("sepa.xml");
    }

    // ---------------------------------------------------------------
    // GET /sepa/export/{id} - error cases
    // ---------------------------------------------------------------

    [Fact]
    public async Task Export_WithoutCreditorConfig_ReturnsNotFound()
    {
        // Arrange
        Guid exportId = Guid.NewGuid();
        MemberEntity member = await CreateMemberAsync(1);
        await CreateContributionAsync(member, exportId);

        // Act
        HttpResponseMessage response = await CreateClient(UserRole.ADMIN).GetAsync($"/sepa/export/{exportId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ErrorDetailsResult? error = await response.Content.ReadFromJsonAsync<ErrorDetailsResult>();
        error!.ErrorCode.ShouldBe(ApiErrorCodes.RESOURCE_NOT_FOUND);
    }

    [Fact]
    public async Task Export_WithDeletedCreditorConfig_ReturnsNotFound()
    {
        // Arrange
        await CreateCreditorConfigAsync(deleted: true);
        Guid exportId = Guid.NewGuid();
        MemberEntity member = await CreateMemberAsync(1);
        await CreateContributionAsync(member, exportId);

        // Act
        HttpResponseMessage response = await CreateClient(UserRole.ADMIN).GetAsync($"/sepa/export/{exportId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("DE00370400440532013000", "Iban")]
    [InlineData("INVALID", "Iban")]
    public async Task Export_WithInvalidCreditorIban_ReturnsBadRequest(string iban, string expectedField)
    {
        // Arrange
        await CreateCreditorConfigAsync(iban: iban);
        Guid exportId = Guid.NewGuid();
        MemberEntity member = await CreateMemberAsync(1);
        await CreateContributionAsync(member, exportId);

        // Act
        HttpResponseMessage response = await CreateClient(UserRole.ADMIN).GetAsync($"/sepa/export/{exportId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ErrorDetailsResult? error = await response.Content.ReadFromJsonAsync<ErrorDetailsResult>();
        error!.MoreInfo.ShouldNotBeNull();
        error.MoreInfo.ShouldContain(expectedField);
    }

    [Theory]
    [InlineData("")]
    [InlineData("DEU")]
    [InlineData("1A")]
    public async Task Export_WithInvalidCreditorCountry_ReturnsBadRequest(string country)
    {
        // Arrange
        await CreateCreditorConfigAsync(country: country);
        Guid exportId = Guid.NewGuid();
        MemberEntity member = await CreateMemberAsync(1);
        await CreateContributionAsync(member, exportId);

        // Act
        HttpResponseMessage response = await CreateClient(UserRole.ADMIN).GetAsync($"/sepa/export/{exportId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ErrorDetailsResult? error = await response.Content.ReadFromJsonAsync<ErrorDetailsResult>();
        error!.MoreInfo!.ShouldContain("Country");
    }

    [Fact]
    public async Task Export_WithUnknownExportId_ReturnsUnprocessableEntity()
    {
        // Arrange
        await CreateCreditorConfigAsync();
        MemberEntity member = await CreateMemberAsync(1);
        await CreateContributionAsync(member, Guid.NewGuid());

        // Act
        HttpResponseMessage response = await CreateClient(UserRole.ADMIN).GetAsync($"/sepa/export/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        ErrorDetailsResult? error = await response.Content.ReadFromJsonAsync<ErrorDetailsResult>();
        error!.ErrorCode.ShouldBe(ApiErrorCodes.UNPROCESSABLE_ENTITY);
    }

    [Fact]
    public async Task Export_WhenOnlyNonCollectibleContributions_ReturnsUnprocessableEntity()
    {
        // Arrange
        await CreateCreditorConfigAsync();
        Guid exportId = Guid.NewGuid();
        await CreateContributionAsync(await CreateMemberAsync(1), exportId, paid: DateTimeOffset.UtcNow);
        await CreateContributionAsync(await CreateMemberAsync(2), exportId, dueDate: DateTime.Today.AddDays(10));
        await CreateContributionAsync(await CreateMemberAsync(3, sepaConsent: false), exportId);
        await CreateContributionAsync(await CreateMemberAsync(4, iban: null), exportId);
        await CreateContributionAsync(await CreateMemberAsync(5), exportId, amount: 0m);

        // Act
        HttpResponseMessage response = await CreateClient(UserRole.ADMIN).GetAsync($"/sepa/export/{exportId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    // ---------------------------------------------------------------
    // GET /sepa/export/{id} - file content
    // ---------------------------------------------------------------

    [Fact]
    public async Task Export_FileIsUtf8WithoutBom_AndUsesPain00800108()
    {
        // Arrange
        Guid exportId = await CreateDefaultExportAsync();

        // Act
        byte[] bytes = await GetExportBytesAsync(exportId);

        // Assert
        ((char)bytes[0]).ShouldBe('<', "file must not start with a UTF-8 byte order mark");
        XDocument doc = XDocument.Parse(System.Text.Encoding.UTF8.GetString(bytes));
        doc.Root!.Name.ShouldBe(Ns + "Document");
        doc.Declaration!.Encoding.ShouldBe("utf-8", StringCompareShould.IgnoreCase);
    }

    [Fact]
    public async Task Export_GroupHeader_HasConsistentCountsAndUtcTimestamp()
    {
        // Arrange
        await CreateCreditorConfigAsync();
        Guid exportId = Guid.NewGuid();
        await CreateContributionAsync(await CreateMemberAsync(1), exportId, amount: 12.5m);
        await CreateContributionAsync(await CreateMemberAsync(2), exportId, amount: 30m);

        // Act
        XDocument doc = await GetExportAsync(exportId);

        // Assert
        XElement header = doc.Descendants(Ns + "GrpHdr").Single();
        header.Element(Ns + "NbOfTxs")!.Value.ShouldBe("2");
        header.Element(Ns + "CtrlSum")!.Value.ShouldBe("42.50");
        header.Element(Ns + "CreDtTm")!.Value.ShouldMatch(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$");
        header.Element(Ns + "MsgId")!.Value.Length.ShouldBeLessThanOrEqualTo(35);

        XElement paymentInfo = doc.Descendants(Ns + "PmtInf").Single();
        paymentInfo.Element(Ns + "NbOfTxs")!.Value.ShouldBe("2");
        paymentInfo.Element(Ns + "CtrlSum")!.Value.ShouldBe("42.50");
        decimal sum = doc.Descendants(Ns + "InstdAmt").Sum(x => decimal.Parse(x.Value, CultureInfo.InvariantCulture));
        sum.ShouldBe(42.5m);
    }

    [Fact]
    public async Task Export_UsesCoreRecurringDirectDebit()
    {
        // Arrange
        Guid exportId = await CreateDefaultExportAsync();

        // Act
        XDocument doc = await GetExportAsync(exportId);

        // Assert
        XElement paymentType = doc.Descendants(Ns + "PmtTpInf").Single();
        paymentType.Element(Ns + "SvcLvl")!.Element(Ns + "Cd")!.Value.ShouldBe("SEPA");
        paymentType.Element(Ns + "LclInstrm")!.Element(Ns + "Cd")!.Value.ShouldBe("CORE");
        paymentType.Element(Ns + "SeqTp")!.Value.ShouldBe("RCUR");
        doc.Descendants(Ns + "ChrgBr").Single().Value.ShouldBe("SLEV");
        doc.Descendants(Ns + "CdtrSchmeId").Single().Descendants(Ns + "Id").Last().Value
            .ShouldBe("DE98ZZZ09999999999");
    }

    [Fact]
    public async Task Export_CreditorAddress_IsFullyStructuredFromConfig()
    {
        // Arrange
        await CreateCreditorConfigAsync(street: "Vereinsstraße 1", postCode: "12345", city: "Musterstadt");
        Guid exportId = Guid.NewGuid();
        await CreateContributionAsync(await CreateMemberAsync(1), exportId);

        // Act
        XDocument doc = await GetExportAsync(exportId);

        // Assert
        XElement address = doc.Descendants(Ns + "Cdtr").Single().Element(Ns + "PstlAdr")!;
        address.Element(Ns + "StrtNm")!.Value.ShouldBe("Vereinsstrasse 1");
        address.Element(Ns + "PstCd")!.Value.ShouldBe("12345");
        address.Element(Ns + "TwnNm")!.Value.ShouldBe("Musterstadt");
        address.Element(Ns + "Ctry")!.Value.ShouldBe("DE");
        address.Elements(Ns + "AdrLine").ShouldBeEmpty();
        address.Elements().Select(x => x.Name.LocalName).ShouldBe(new[] { "StrtNm", "PstCd", "TwnNm", "Ctry" });
    }

    [Fact]
    public async Task Export_CreditorAddress_WithoutStreetAndPostCode_OnlyContainsTownAndCountry()
    {
        // Arrange
        await CreateCreditorConfigAsync(street: null, postCode: null);
        Guid exportId = Guid.NewGuid();
        await CreateContributionAsync(await CreateMemberAsync(1), exportId);

        // Act
        XDocument doc = await GetExportAsync(exportId);

        // Assert
        XElement address = doc.Descendants(Ns + "Cdtr").Single().Element(Ns + "PstlAdr")!;
        address.Elements().Select(x => x.Name.LocalName).ShouldBe(new[] { "TwnNm", "Ctry" });
    }

    [Fact]
    public async Task Export_DebtorAddress_IsFullyStructured()
    {
        // Arrange
        await CreateCreditorConfigAsync();
        Guid exportId = Guid.NewGuid();
        await CreateContributionAsync(await CreateMemberAsync(1, street: "Musterweg 7a", postCode: "54321",
            city: "Beispielstadt", countryCode: "at"), exportId);

        // Act
        XDocument doc = await GetExportAsync(exportId);

        // Assert
        XElement address = doc.Descendants(Ns + "Dbtr").Single().Element(Ns + "PstlAdr")!;
        address.Element(Ns + "StrtNm")!.Value.ShouldBe("Musterweg 7a");
        address.Element(Ns + "PstCd")!.Value.ShouldBe("54321");
        address.Element(Ns + "TwnNm")!.Value.ShouldBe("Beispielstadt");
        address.Element(Ns + "Ctry")!.Value.ShouldBe("AT");
        address.Elements(Ns + "AdrLine").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("", "DE")]
    [InlineData("Musterstadt", null)]
    [InlineData("Musterstadt", "Germany")]
    public async Task Export_DebtorAddress_IsOmitted_WhenTownOrCountryIsMissing(string city, string? countryCode)
    {
        // Arrange
        await CreateCreditorConfigAsync();
        Guid exportId = Guid.NewGuid();
        await CreateContributionAsync(await CreateMemberAsync(1, city: city, countryCode: countryCode), exportId);

        // Act
        XDocument doc = await GetExportAsync(exportId);

        // Assert
        XElement debtor = doc.Descendants(Ns + "Dbtr").Single();
        debtor.Element(Ns + "PstlAdr").ShouldBeNull();
        debtor.Element(Ns + "Nm")!.Value.ShouldBe("Max Mustermann");
        doc.Descendants(Ns + "TwnNm").ShouldAllBe(x => x.Value.Length > 0);
    }

    [Fact]
    public async Task Export_TruncatesFieldsToSepaLengths()
    {
        // Arrange
        await CreateCreditorConfigAsync(name: new string('V', 120));
        Guid exportId = Guid.NewGuid();
        await CreateContributionAsync(await CreateMemberAsync(1, firstName: new string('A', 50),
            lastName: new string('B', 50), street: new string('S', 90), city: new string('C', 50)), exportId);

        // Act
        XDocument doc = await GetExportAsync(exportId);

        // Assert
        doc.Descendants(Ns + "Nm").ShouldAllBe(x => x.Value.Length <= 70);
        doc.Descendants(Ns + "StrtNm").ShouldAllBe(x => x.Value.Length <= 70);
        doc.Descendants(Ns + "TwnNm").ShouldAllBe(x => x.Value.Length <= 35);
        doc.Descendants(Ns + "Cdtr").Single().Element(Ns + "Nm")!.Value.ShouldBe(new string('V', 70));
        doc.ToString().ShouldNotContain("…");
    }

    [Fact]
    public async Task Export_ConvertsCharactersOutsideSepaCharacterSet()
    {
        // Arrange
        await CreateCreditorConfigAsync(name: "Verein_für Café & Kultur e.V.");
        Guid exportId = Guid.NewGuid();
        await CreateContributionAsync(await CreateMemberAsync(1, firstName: "Zoë", lastName: "Øster-Müller"), exportId);

        // Act
        XDocument doc = await GetExportAsync(exportId);

        // Assert
        doc.Descendants(Ns + "Cdtr").Single().Element(Ns + "Nm")!.Value.ShouldBe("Verein-fuer Cafe + Kultur e.V.");
        doc.Descendants(Ns + "Dbtr").Single().Element(Ns + "Nm")!.Value.ShouldBe("Zoe Oster-Mueller");
        doc.Descendants().Where(x => !x.HasElements).ShouldAllBe(x => System.Text.RegularExpressions.Regex.IsMatch(
            x.Value, @"^[a-zA-Z0-9/\-?:().,'+ ]*$"));
    }

    [Fact]
    public async Task Export_MultipleContributionsOfSameMember_HaveUniqueIds()
    {
        // Arrange
        await CreateCreditorConfigAsync();
        Guid exportId = Guid.NewGuid();
        MemberEntity member = await CreateMemberAsync(7, interval: Interval.MONTHLY);
        await CreateContributionAsync(member, exportId, dueDate: DateTime.Today.AddMonths(-2));
        await CreateContributionAsync(member, exportId, dueDate: DateTime.Today.AddMonths(-1));
        await CreateContributionAsync(member, exportId, dueDate: DateTime.Today.AddMonths(-1));

        // Act
        XDocument doc = await GetExportAsync(exportId);

        // Assert
        List<string> instrIds = doc.Descendants(Ns + "InstrId").Select(x => x.Value).ToList();
        List<string> endToEndIds = doc.Descendants(Ns + "EndToEndId").Select(x => x.Value).ToList();
        instrIds.Count.ShouldBe(3);
        instrIds.Distinct().Count().ShouldBe(3);
        endToEndIds.Distinct().Count().ShouldBe(3);
        instrIds.Concat(endToEndIds).ShouldAllBe(x => x.Length <= 35 && !x.Contains(' '));
    }

    [Fact]
    public async Task Export_MandateReference_UsesSepaCharacterSet()
    {
        // Arrange
        await CreateCreditorConfigAsync();
        Guid exportId = Guid.NewGuid();
        await CreateContributionAsync(await CreateMemberAsync(1, mandateId: "20240101120000_1"), exportId);

        // Act
        XDocument doc = await GetExportAsync(exportId);

        // Assert
        XElement mandate = doc.Descendants(Ns + "MndtRltdInf").Single();
        mandate.Element(Ns + "MndtId")!.Value.ShouldBe("20240101120000-1");
        mandate.Element(Ns + "AmdmntInd")!.Value.ShouldBe("false");
        mandate.Element(Ns + "DtOfSgntr")!.Value.ShouldMatch(@"^\d{4}-\d{2}-\d{2}$");
    }

    [Fact]
    public async Task Export_MemberWithoutBic_IsExportedWithNotProvidedAgent()
    {
        // Arrange
        await CreateCreditorConfigAsync();
        Guid exportId = Guid.NewGuid();
        await CreateContributionAsync(await CreateMemberAsync(1, bic: null), exportId);
        await CreateContributionAsync(await CreateMemberAsync(2, bic: ""), exportId);

        // Act
        XDocument doc = await GetExportAsync(exportId);

        // Assert
        List<XElement> agents = doc.Descendants(Ns + "DbtrAgt").ToList();
        agents.Count.ShouldBe(2);
        foreach (XElement agent in agents)
        {
            XElement institution = agent.Element(Ns + "FinInstnId")!;
            institution.Element(Ns + "BICFI").ShouldBeNull();
            institution.Element(Ns + "Othr")!.Element(Ns + "Id")!.Value.ShouldBe("NOTPROVIDED");
        }
    }

    [Fact]
    public async Task Export_ProvidesKnownBics_Normalized()
    {
        // Arrange
        await CreateCreditorConfigAsync(bic: "coba deff xxx");
        Guid exportId = Guid.NewGuid();
        await CreateContributionAsync(await CreateMemberAsync(1, iban: "de02 1203 0000 0000 2020 51", bic: "byla dem1 001"), exportId);

        // Act
        XDocument doc = await GetExportAsync(exportId);

        // Assert
        doc.Descendants(Ns + "DbtrAcct").Single().Descendants(Ns + "IBAN").Single().Value.ShouldBe(MemberIban);
        doc.Descendants(Ns + "CdtrAgt").Single().Descendants(Ns + "BICFI").Single().Value.ShouldBe(CreditorBic);
        doc.Descendants(Ns + "DbtrAgt").Single().Descendants(Ns + "BICFI").Single().Value.ShouldBe(MemberBic);
        doc.Descendants(Ns + "Othr").Where(x => x.Parent!.Name == Ns + "FinInstnId").ShouldBeEmpty();
    }

    [Fact]
    public async Task Export_MemberWithInvalidBic_IsExportedWithNotProvidedAgent()
    {
        // Arrange
        await CreateCreditorConfigAsync();
        Guid exportId = Guid.NewGuid();
        await CreateContributionAsync(await CreateMemberAsync(1, bic: "INVALID"), exportId);

        // Act
        XDocument doc = await GetExportAsync(exportId);

        // Assert
        XElement institution = doc.Descendants(Ns + "DbtrAgt").Single().Element(Ns + "FinInstnId")!;
        institution.Element(Ns + "BICFI").ShouldBeNull();
        institution.Element(Ns + "Othr")!.Element(Ns + "Id")!.Value.ShouldBe("NOTPROVIDED");
    }

    [Fact]
    public async Task Export_DebtorPspOutsideEea_ProvidesBicAndAddress()
    {
        // Arrange
        await CreateCreditorConfigAsync();
        Guid exportId = Guid.NewGuid();
        await CreateContributionAsync(await CreateMemberAsync(1, iban: SwissIban, bic: "ubsw chzh 80a",
            street: "Bahnhofstrasse 1", postCode: "8001", city: "Zürich", countryCode: "CH"), exportId);

        // Act
        XDocument doc = await GetExportAsync(exportId);

        // Assert
        doc.Descendants(Ns + "DbtrAgt").Single().Descendants(Ns + "BICFI").Single().Value.ShouldBe(SwissBic);
        XElement address = doc.Descendants(Ns + "Dbtr").Single().Element(Ns + "PstlAdr")!;
        address.Element(Ns + "TwnNm")!.Value.ShouldBe("Zuerich");
        address.Element(Ns + "Ctry")!.Value.ShouldBe("CH");
    }

    [Fact]
    public async Task Export_CreditorPspOutsideEea_RequiresDebtorAddress()
    {
        // Arrange
        await CreateCreditorConfigAsync(iban: SwissIban, bic: SwissBic, country: "CH");
        Guid exportId = Guid.NewGuid();
        await CreateContributionAsync(await CreateMemberAsync(1, bic: MemberBic), exportId, amount: 10m);
        await CreateContributionAsync(await CreateMemberAsync(2, city: ""), exportId, amount: 20m);

        // Act
        XDocument doc = await GetExportAsync(exportId);

        // Assert
        doc.Descendants(Ns + "CdtrAgt").Single().Descendants(Ns + "BICFI").Single().Value.ShouldBe(SwissBic);
        doc.Descendants(Ns + "DrctDbtTxInf").Count().ShouldBe(1);
        doc.Descendants(Ns + "DbtrAgt").Single().Descendants(Ns + "BICFI").Single().Value.ShouldBe(MemberBic);
        doc.Descendants(Ns + "Dbtr").Single().Element(Ns + "PstlAdr").ShouldNotBeNull();
    }

    [Fact]
    public async Task Export_DebtorPspOutsideEea_WithoutAddress_IsSkipped()
    {
        // Arrange
        await CreateCreditorConfigAsync();
        Guid exportId = Guid.NewGuid();
        await CreateContributionAsync(await CreateMemberAsync(1), exportId, amount: 10m);
        await CreateContributionAsync(await CreateMemberAsync(2, iban: SwissIban, bic: SwissBic, countryCode: null), exportId, amount: 20m);

        // Act
        XDocument doc = await GetExportAsync(exportId);

        // Assert
        doc.Descendants(Ns + "InstdAmt").Select(x => x.Value).ShouldBe(new[] { "10.00" });
    }

    [Theory]
    [InlineData("")]
    [InlineData("COBADEFF1")]
    public async Task Export_WithMissingOrInvalidCreditorBic_ReturnsBadRequest(string bic)
    {
        // Arrange
        await CreateCreditorConfigAsync(bic: bic);
        Guid exportId = Guid.NewGuid();
        await CreateContributionAsync(await CreateMemberAsync(1), exportId);

        // Act
        HttpResponseMessage response = await CreateClient(UserRole.ADMIN).GetAsync($"/sepa/export/{exportId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ErrorDetailsResult? error = await response.Content.ReadFromJsonAsync<ErrorDetailsResult>();
        error!.MoreInfo!.ShouldContain("Bic");
    }

    [Fact]
    public async Task Export_CreditorSchemeId_IsNormalized()
    {
        // Arrange
        await CreateCreditorConfigAsync(creditorId: "de98 zzz 0999 9999 999");
        Guid exportId = Guid.NewGuid();
        await CreateContributionAsync(await CreateMemberAsync(1), exportId);

        // Act
        XDocument doc = await GetExportAsync(exportId);

        // Assert
        XElement other = doc.Descendants(Ns + "CdtrSchmeId").Single().Descendants(Ns + "Othr").Single();
        other.Element(Ns + "Id")!.Value.ShouldBe("DE98ZZZ09999999999");
        other.Element(Ns + "SchmeNm")!.Element(Ns + "Prtry")!.Value.ShouldBe("SEPA");
    }

    [Fact]
    public async Task Export_WithInvalidCreditorId_ReturnsBadRequest()
    {
        // Arrange
        await CreateCreditorConfigAsync(creditorId: "DE97ZZZ09999999999");
        Guid exportId = Guid.NewGuid();
        await CreateContributionAsync(await CreateMemberAsync(1), exportId);

        // Act
        HttpResponseMessage response = await CreateClient(UserRole.ADMIN).GetAsync($"/sepa/export/{exportId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ErrorDetailsResult? error = await response.Content.ReadFromJsonAsync<ErrorDetailsResult>();
        error!.MoreInfo!.ShouldContain("CreditorId");
    }

    // ---------------------------------------------------------------
    // GET /sepa/export/{id} - validation against the ISO 20022 schema and the EPC SDD Core rules
    // ---------------------------------------------------------------

    [Fact]
    public async Task Export_WithMixedData_IsValidSepaFile()
    {
        // Arrange
        await CreateCreditorConfigAsync(name: "Verein für Café & Kultur e.V. " + new string('X', 80));
        Guid exportId = Guid.NewGuid();
        MemberEntity monthly = await CreateMemberAsync(1, firstName: "Zoë", lastName: "Øster-Müller", interval: Interval.MONTHLY);
        await CreateContributionAsync(monthly, exportId, amount: 12.5m, dueDate: DateTime.Today.AddMonths(-1));
        await CreateContributionAsync(monthly, exportId, amount: 12.5m);
        await CreateContributionAsync(await CreateMemberAsync(2, bic: null, street: "", postCode: ""), exportId, amount: 999999999.99m);
        await CreateContributionAsync(await CreateMemberAsync(3, city: "", countryCode: null), exportId, amount: 0.01m);
        await CreateContributionAsync(await CreateMemberAsync(4, iban: SwissIban, bic: SwissBic, city: "Zürich",
            countryCode: "CH", mandateId: "/MANDAT//4 mit Leerzeichen und überlanger Referenz/"), exportId);

        // Act
        XDocument doc = await GetExportAsync(exportId);

        // Assert
        doc.Descendants(Ns + "DrctDbtTxInf").Count().ShouldBe(5);
        ShouldBeValidSepaFile(doc);
    }

    [Fact]
    public async Task Export_WithMinimalData_IsValidSepaFile()
    {
        // Arrange
        await CreateCreditorConfigAsync(street: null, postCode: null);
        Guid exportId = Guid.NewGuid();
        await CreateContributionAsync(await CreateMemberAsync(1, city: "", bic: null), exportId);

        // Act
        XDocument doc = await GetExportAsync(exportId);

        // Assert
        ShouldBeValidSepaFile(doc);
    }

    [Fact]
    public async Task Export_WithCreditorOutsideEea_IsValidSepaFile()
    {
        // Arrange
        await CreateCreditorConfigAsync(iban: SwissIban, bic: SwissBic, country: "CH", city: "Basel");
        Guid exportId = Guid.NewGuid();
        await CreateContributionAsync(await CreateMemberAsync(1), exportId);
        await CreateContributionAsync(await CreateMemberAsync(2, bic: null), exportId);

        // Act
        XDocument doc = await GetExportAsync(exportId);

        // Assert
        ShouldBeValidSepaFile(doc);
    }

    [Fact]
    public void IsoSchema_ReportsErrorsForInvalidDocument()
    {
        // Arrange
        XDocument invalid = XDocument.Parse($"""
            <Document xmlns="{Ns.NamespaceName}"><CstmrDrctDbtInitn><GrpHdr><MsgId>1</MsgId></GrpHdr></CstmrDrctDbtInitn></Document>
            """);

        // Act & Assert
        ValidateAgainstIsoSchema(invalid).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Export_SkipsMembersWithInvalidIban_AndZeroAmounts()
    {
        // Arrange
        await CreateCreditorConfigAsync();
        Guid exportId = Guid.NewGuid();
        await CreateContributionAsync(await CreateMemberAsync(1), exportId, amount: 10m);
        await CreateContributionAsync(await CreateMemberAsync(2, iban: "DE00120300000000202051"), exportId, amount: 20m);
        await CreateContributionAsync(await CreateMemberAsync(3), exportId, amount: 0m);

        // Act
        XDocument doc = await GetExportAsync(exportId);

        // Assert
        doc.Descendants(Ns + "DrctDbtTxInf").Count().ShouldBe(1);
        doc.Descendants(Ns + "GrpHdr").Single().Element(Ns + "CtrlSum")!.Value.ShouldBe("10.00");
    }

    [Fact]
    public async Task Export_OnlyContainsContributionsOfRequestedExport()
    {
        // Arrange
        await CreateCreditorConfigAsync();
        Guid exportId = Guid.NewGuid();
        await CreateContributionAsync(await CreateMemberAsync(1), exportId, amount: 10m);
        await CreateContributionAsync(await CreateMemberAsync(2), Guid.NewGuid(), amount: 99m);

        // Act
        XDocument doc = await GetExportAsync(exportId);

        // Assert
        doc.Descendants(Ns + "InstdAmt").Select(x => x.Value).ShouldBe(new[] { "10.00" });
    }

    [Fact]
    public async Task Export_RemittanceInformation_ContainsBillingPeriod()
    {
        // Arrange
        await CreateCreditorConfigAsync();
        Guid exportId = Guid.NewGuid();
        DateTime dueDate = new(DateTime.Today.Year, DateTime.Today.Month, 1);
        await CreateContributionAsync(await CreateMemberAsync(1, interval: Interval.MONTHLY), exportId, dueDate: dueDate);

        // Act
        XDocument doc = await GetExportAsync(exportId);

        // Assert
        doc.Descendants(Ns + "Ustrd").Single().Value.ShouldBe($"Membership fee {dueDate:yyyy-MM}");
    }

    // ---------------------------------------------------------------
    // GET /sepa/exports
    // ---------------------------------------------------------------

    [Fact]
    public async Task Exports_WithoutToken_ReturnsUnauthorized()
    {
        // Act
        HttpResponseMessage response = await CreateClient().GetAsync("/sepa/exports");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Exports_AsUser_ReturnsForbidden()
    {
        // Act
        HttpResponseMessage response = await CreateClient(UserRole.USER).GetAsync("/sepa/exports");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Exports_WhenNoneExist_ReturnsNotFound()
    {
        // Act
        HttpResponseMessage response = await CreateClient(UserRole.ADMIN).GetAsync("/sepa/exports");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task Exports_ReturnsExportsNewestFirst(UserRole role)
    {
        // Arrange
        SepaExportEntity older = await CreateSepaExportAsync("older.xml", DateTimeOffset.UtcNow.AddMonths(-1), 10m, 1);
        SepaExportEntity newer = await CreateSepaExportAsync("newer.xml", DateTimeOffset.UtcNow, 25m, 2);

        // Act
        HttpResponseMessage response = await CreateClient(role).GetAsync("/sepa/exports");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        AllSepaExportInfoResults? result = await response.Content.ReadFromJsonAsync<AllSepaExportInfoResults>();
        result!.Total.ShouldBe(2);
        result.Items.Select(x => x.Id).ShouldBe(new[] { newer.Id, older.Id });
        result.Items[0].Name.ShouldBe("newer.xml");
        result.Items[0].Amount.ShouldBe(25m);
        result.Items[0].ExportedCases.ShouldBe(2);
    }

    [Fact]
    public async Task Exports_SupportsPaging()
    {
        // Arrange
        foreach (int index in Enumerable.Range(0, 5))
            await CreateSepaExportAsync($"export-{index}.xml", DateTimeOffset.UtcNow.AddDays(-index), 1m, 1);

        // Act
        HttpResponseMessage response = await CreateClient(UserRole.ADMIN).GetAsync("/sepa/exports?offset=1&limit=2");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        AllSepaExportInfoResults? result = await response.Content.ReadFromJsonAsync<AllSepaExportInfoResults>();
        result!.Total.ShouldBe(5);
        result.Items.Select(x => x.Name).ShouldBe(new[] { "export-1.xml", "export-2.xml" });
    }

    [Fact]
    public async Task Exports_IgnoresDeletedExports()
    {
        // Arrange
        await CreateSepaExportAsync("active.xml", DateTimeOffset.UtcNow, 1m, 1);
        await CreateSepaExportAsync("deleted.xml", DateTimeOffset.UtcNow, 1m, 1, deleted: true);

        // Act
        HttpResponseMessage response = await CreateClient(UserRole.ADMIN).GetAsync("/sepa/exports");

        // Assert
        AllSepaExportInfoResults? result = await response.Content.ReadFromJsonAsync<AllSepaExportInfoResults>();
        result!.Total.ShouldBe(1);
        result.Items.Single().Name.ShouldBe("active.xml");
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private async Task<Guid> CreateDefaultExportAsync()
    {
        await CreateCreditorConfigAsync();
        Guid exportId = Guid.NewGuid();
        await CreateContributionAsync(await CreateMemberAsync(1), exportId);
        return exportId;
    }

    private static void ShouldBeValidSepaFile(XDocument doc)
    {
        List<string> errors = ValidateAgainstIsoSchema(doc).Concat(EpcSddCoreRules.Validate(doc)).ToList();
        errors.ShouldBeEmpty($"{string.Join("\n", errors)}\n\n{doc}");
    }

    private static List<string> ValidateAgainstIsoSchema(XDocument doc)
    {
        XmlSchemaSet schemas = new();
        schemas.Add(Ns.NamespaceName, Path.Combine(AppContext.BaseDirectory, "Resources", "Sepa", IsoSchema));

        List<string> errors = [];
        doc.Validate(schemas, (_, e) => errors.Add($"{e.Severity}: {e.Message}"));
        return errors;
    }

    private async Task<byte[]> GetExportBytesAsync(Guid exportId)
    {
        HttpResponseMessage response = await CreateClient(UserRole.ADMIN).GetAsync($"/sepa/export/{exportId}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadAsByteArrayAsync();
    }

    private async Task<XDocument> GetExportAsync(Guid exportId)
    {
        byte[] bytes = await GetExportBytesAsync(exportId);
        return XDocument.Parse(System.Text.Encoding.UTF8.GetString(bytes));
    }

    private async Task CreateCreditorConfigAsync(string name = "Musterverein e.V.", string iban = CreditorIban,
        string bic = CreditorBic, string? street = "Vereinsstraße 1", string? postCode = "12345",
        string city = "Musterstadt", string country = "DE", string creditorId = "DE98ZZZ09999999999",
        bool deleted = false)
    {
        await WithDbContext(async db =>
        {
            db.CreditorConfigs.Add(new CreditorConfigEntity
            {
                Id = Guid.NewGuid(),
                Name = name,
                Iban_Encrypted = _crypto.Encrypt(iban),
                Bic_Encrypted = _crypto.Encrypt(bic),
                CreditorId = creditorId,
                StreetNameAndNumber = street,
                PostCode = postCode,
                CityName = city,
                CountryCode = country,
                DeletedAt = deleted ? DateTimeOffset.UtcNow : null
            });
            await db.ForceSaveChangesAsync();
        });
    }

    private async Task<MemberEntity> CreateMemberAsync(int memberNumber, string? mandateId = null,
        string firstName = "Max", string lastName = "Mustermann", string street = "Musterstraße 1",
        string postCode = "12345", string city = "Musterstadt", string? countryCode = "DE",
        string? iban = MemberIban, string? bic = MemberBic, bool sepaConsent = true,
        Interval interval = Interval.YEARLY)
    {
        ContributionPlanEntity plan = new() { Id = Guid.NewGuid(), Interval = interval };
        MemberEntity member = new()
        {
            Id = Guid.NewGuid(),
            MemberNumber = memberNumber,
            MandateId = mandateId ?? $"20240101120000_{memberNumber}",
            FirstName = firstName,
            LastName = lastName,
            BirthdayEncrypted = string.Empty,
            StreetEncrypted = _crypto.Encrypt(street),
            PostalCode = postCode,
            City = city,
            CountryCode = countryCode,
            IBAN_Encrypted = iban == null ? null : _crypto.Encrypt(iban),
            Bic_Encrypted = bic == null ? null : _crypto.Encrypt(bic),
            SepaConsent = sepaConsent ? DateTimeOffset.UtcNow.AddYears(-1) : null,
            ContributionPlanId = plan.Id
        };

        await WithDbContext(async db =>
        {
            db.ContributionPlans.Add(plan);
            db.Members.Add(member);
            await db.ForceSaveChangesAsync();
        });

        return member;
    }

    private async Task CreateContributionAsync(MemberEntity member, Guid exportId, decimal amount = 120m,
        DateTime? dueDate = null, DateTimeOffset? paid = null)
    {
        await WithDbContext(async db =>
        {
            MemberEntity trackedMember = await db.Members.FindAsync(member.Id) ?? throw new InvalidOperationException();
            db.Contributions.Add(new ContributionEntity
            {
                Id = Guid.NewGuid(),
                MemberId = member.Id,
                MemberEntity = trackedMember,
                ExportId = exportId,
                Amount = amount,
                DueDate = dueDate ?? DateTime.Today.AddDays(-1),
                Paid = paid
            });
            await db.ForceSaveChangesAsync();
        });
    }

    private async Task<SepaExportEntity> CreateSepaExportAsync(string name, DateTimeOffset createdAt, decimal amount,
        int count, bool deleted = false)
    {
        SepaExportEntity export = new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Amount = amount,
            Count = count,
            CreatedAt = createdAt,
            DeletedAt = deleted ? DateTimeOffset.UtcNow : null
        };

        await WithDbContext(async db =>
        {
            db.SepaExports.Add(export);
            await db.ForceSaveChangesAsync();
        });

        return export;
    }

    private async Task ClearSepaDataAsync()
    {
        await WithDbContext(async db =>
        {
            db.Contributions.RemoveRange(db.Contributions.IgnoreQueryFilters());
            db.Members.RemoveRange(db.Members.IgnoreQueryFilters());
            db.ContributionPlans.RemoveRange(db.ContributionPlans.IgnoreQueryFilters());
            db.CreditorConfigs.RemoveRange(db.CreditorConfigs.IgnoreQueryFilters());
            db.SepaExports.RemoveRange(db.SepaExports.IgnoreQueryFilters());
            await db.ForceSaveChangesAsync();
        });
    }
}
