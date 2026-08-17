using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.ApiResults.Receipt;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Models.Enums;
using UniVerein.Api.Services;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Shouldly;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class ReceiptsControllerTests : IntegrationTestBase
{
    private readonly AppDbContext _db;

    private static readonly byte[] MinimalPdfBytes = Encoding.ASCII.GetBytes(
        "%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF");

    // The server serializes enums as strings (see Startup.ConfigureServices -> AddJsonOptions), but
    // HttpContent's JSON extension methods use plain System.Text.Json defaults unless told otherwise.
    // Needed whenever a (de-)serialized DTO carries an enum property, e.g. ReceiptResult.PaymentMethod.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public ReceiptsControllerTests(UniVereinWebApplicationFactory factory)
        : base(factory)
    {
        _db = GetService<AppDbContext>();
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _db.ReceiptFiles.RemoveRange(_db.ReceiptFiles.IgnoreQueryFilters());
        _db.Receipts.RemoveRange(_db.Receipts.IgnoreQueryFilters());
        _db.ReceiptCategories.RemoveRange(_db.ReceiptCategories.IgnoreQueryFilters());
        await _db.ForceSaveChangesAsync();

        _db.Users.Add(new UserEntity
        {
            Id = UserId,
            Username = "receipt-tester",
            PasswordHash = CryptoService.HashPassword("Test1234!"),
            Role = UserRole.ADMIN
        });
        await _db.SaveChangesAsync();
    }

    private static MultipartFormDataContent CreateReceiptFormData(
        byte[]? fileBytes = null,
        string fileName = "receipt.pdf",
        string contentType = "application/pdf",
        string amount = "10.00")
    {
        MultipartFormDataContent content = new()
        {
            { new StringContent(amount), "amount" },
            { new StringContent(DateTime.UtcNow.ToString("O")), "receiptDate" }
        };

        if (fileBytes != null)
        {
            ByteArrayContent fileContent = new(fileBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            content.Add(fileContent, "files", fileName);
        }

        return content;
    }

    private static async Task<ReceiptResult> CreateReceiptAsync(HttpClient client, decimal amount,
        DateTime receiptDate, Guid? categoryId = null)
    {
        using MultipartFormDataContent content = new()
        {
            { new StringContent(amount.ToString(CultureInfo.InvariantCulture)), "amount" },
            { new StringContent(receiptDate.ToString("O")), "receiptDate" }
        };

        if (categoryId != null)
            content.Add(new StringContent(categoryId.Value.ToString()), "categoryId");

        HttpResponseMessage response = await client.PostAsync("/receipts", content);
        ReceiptResult? result = await response.Content.ReadFromJsonAsync<ReceiptResult>();
        result.ShouldNotBeNull();

        return result;
    }

    [Fact]
    public async Task CreateAsync_WithPdfFile_ReturnsCreatedWithPdfFile()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        using MultipartFormDataContent content = CreateReceiptFormData(MinimalPdfBytes);

        // Act
        HttpResponseMessage response = await client.PostAsync("/receipts", content);
        ReceiptResult? result = await response.Content.ReadFromJsonAsync<ReceiptResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        result.ShouldNotBeNull();
        result.Files.Count.ShouldBe(1);
        result.Files[0].ContentType.ShouldBe("application/pdf");
    }

    [Fact]
    public async Task GetFileAsync_ForPdfFile_ReturnsOriginalBytesWithPdfContentType()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        using MultipartFormDataContent content = CreateReceiptFormData(MinimalPdfBytes);
        HttpResponseMessage createResponse = await client.PostAsync("/receipts", content);
        ReceiptResult? created = await createResponse.Content.ReadFromJsonAsync<ReceiptResult>();
        created.ShouldNotBeNull();
        Guid fileId = created.Files[0].Id;

        // Act
        HttpResponseMessage response = await client.GetAsync($"/receipts/{created.Id}/files/{fileId}");
        byte[] bytes = await response.Content.ReadAsByteArrayAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/pdf");
        bytes.ShouldBe(MinimalPdfBytes);
    }

    [Fact]
    public async Task CreateAsync_WithDisallowedFileType_ReturnsBadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        using MultipartFormDataContent content = CreateReceiptFormData(
            Encoding.UTF8.GetBytes("not an image or pdf"),
            fileName: "receipt.txt",
            contentType: "text/plain");

        // Act
        HttpResponseMessage response = await client.PostAsync("/receipts", content);
        ErrorDetailsResult? result = await response.Content.ReadFromJsonAsync<ErrorDetailsResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.ShouldNotBeNull();
        result.MoreInfo.ShouldBe("Only image or PDF files are allowed.");
    }

    [Fact]
    public async Task CreateAsync_WithImageFile_StillWorks()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        using MultipartFormDataContent content = CreateReceiptFormData(
            new byte[] { 0x89, 0x50, 0x4E, 0x47 },
            fileName: "receipt.png",
            contentType: "image/png");

        // Act
        HttpResponseMessage response = await client.PostAsync("/receipts", content);
        ReceiptResult? result = await response.Content.ReadFromJsonAsync<ReceiptResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        result.ShouldNotBeNull();
        result.Files.Count.ShouldBe(1);
        result.Files[0].ContentType.ShouldBe("image/png");
    }

    [Theory]
    [InlineData(UserRole.USER)]
    public async Task ExportAsync_Forbidden_WhenNotPrivileged(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.GetAsync("/receipts/export");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ExportAsync_ReturnsZipWithCsvAndFile_ForReceiptWithPdf()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        using MultipartFormDataContent content = CreateReceiptFormData(MinimalPdfBytes);
        HttpResponseMessage createResponse = await client.PostAsync("/receipts", content);
        ReceiptResult? created = await createResponse.Content.ReadFromJsonAsync<ReceiptResult>();
        created.ShouldNotBeNull();
        Guid fileId = created.Files[0].Id;

        // Act
        HttpResponseMessage response = await client.GetAsync("/receipts/export");
        byte[] zipBytes = await response.Content.ReadAsByteArrayAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/zip");

        using MemoryStream memoryStream = new(zipBytes);
        using ZipArchive archive = new(memoryStream, ZipArchiveMode.Read);

        ZipArchiveEntry? csvEntry = archive.GetEntry("receipts.csv");
        csvEntry.ShouldNotBeNull();
        string csvContent;
        using (StreamReader reader = new(csvEntry.Open(), Encoding.UTF8))
            csvContent = await reader.ReadToEndAsync();
        csvContent.ShouldContain(fileId.ToString());

        ZipArchiveEntry? fileEntry = archive.GetEntry($"files/{fileId}.pdf");
        fileEntry.ShouldNotBeNull();
        using MemoryStream extracted = new();
        await fileEntry.Open().CopyToAsync(extracted);
        extracted.ToArray().ShouldBe(MinimalPdfBytes);
    }

    [Fact]
    public async Task ExportAsync_ReturnsZipWithCorrectExtension_ForReceiptWithImage()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        using MultipartFormDataContent content = CreateReceiptFormData(
            new byte[] { 0x89, 0x50, 0x4E, 0x47 },
            fileName: "receipt.png",
            contentType: "image/png");
        HttpResponseMessage createResponse = await client.PostAsync("/receipts", content);
        ReceiptResult? created = await createResponse.Content.ReadFromJsonAsync<ReceiptResult>();
        created.ShouldNotBeNull();
        Guid fileId = created.Files[0].Id;

        // Act
        HttpResponseMessage response = await client.GetAsync("/receipts/export");
        byte[] zipBytes = await response.Content.ReadAsByteArrayAsync();

        // Assert
        using MemoryStream memoryStream = new(zipBytes);
        using ZipArchive archive = new(memoryStream, ZipArchiveMode.Read);
        archive.GetEntry($"files/{fileId}.png").ShouldNotBeNull();
    }

    [Fact]
    public async Task ExportAsync_ReturnsZipWithAllEntries_ForMultipleReceiptsWithMultipleFiles()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        Guid[] fileIds = new Guid[3];

        using (MultipartFormDataContent content = CreateReceiptFormData(MinimalPdfBytes))
        {
            HttpResponseMessage createResponse = await client.PostAsync("/receipts", content);
            ReceiptResult? created = await createResponse.Content.ReadFromJsonAsync<ReceiptResult>();
            created.ShouldNotBeNull();
            fileIds[0] = created.Files[0].Id;
        }

        using (MultipartFormDataContent content = new()
               {
                   { new StringContent("5.00"), "amount" },
                   { new StringContent(DateTime.UtcNow.ToString("O")), "receiptDate" }
               })
        {
            ByteArrayContent pdfContent = new(MinimalPdfBytes);
            pdfContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            content.Add(pdfContent, "files", "receipt.pdf");

            ByteArrayContent pngContent = new(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
            pngContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(pngContent, "files", "receipt.png");

            HttpResponseMessage createResponse = await client.PostAsync("/receipts", content);
            ReceiptResult? created = await createResponse.Content.ReadFromJsonAsync<ReceiptResult>();
            created.ShouldNotBeNull();
            created.Files.Count.ShouldBe(2);
            fileIds[1] = created.Files[0].Id;
            fileIds[2] = created.Files[1].Id;
        }

        // Act
        HttpResponseMessage response = await client.GetAsync("/receipts/export");
        byte[] zipBytes = await response.Content.ReadAsByteArrayAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using MemoryStream memoryStream = new(zipBytes);
        using ZipArchive archive = new(memoryStream, ZipArchiveMode.Read);

        // 1 CSV manifest + 3 receipt files.
        archive.Entries.Count.ShouldBe(4);

        ZipArchiveEntry? csvEntry = archive.GetEntry("receipts.csv");
        csvEntry.ShouldNotBeNull();
        string csvContent;
        using (StreamReader reader = new(csvEntry.Open(), Encoding.UTF8))
            csvContent = await reader.ReadToEndAsync();
        foreach (Guid fileId in fileIds)
            csvContent.ShouldContain(fileId.ToString());

        archive.GetEntry($"files/{fileIds[0]}.pdf").ShouldNotBeNull();
        archive.GetEntry($"files/{fileIds[1]}.pdf").ShouldNotBeNull();
        archive.GetEntry($"files/{fileIds[2]}.png").ShouldNotBeNull();
    }

    [Fact]
    public async Task RestoreAsync_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsync($"/receipts/{Guid.NewGuid()}", new StringContent(""));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task RestoreAsync_Forbidden_WhenNotAdmin(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.PostAsync($"/receipts/{Guid.NewGuid()}", new StringContent(""));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RestoreAsync_NotFound()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PostAsync($"/receipts/{Guid.NewGuid()}", new StringContent(""));
        ErrorDetailsResult? result = await response.Content.ReadFromJsonAsync<ErrorDetailsResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result.ErrorCode.ShouldBe(ApiErrorCodes.RESOURCE_NOT_FOUND);
    }

    [Fact]
    public async Task RestoreAsync_Success_ClearsDeletedAt()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        using MultipartFormDataContent content = CreateReceiptFormData();
        HttpResponseMessage createResponse = await client.PostAsync("/receipts", content);
        ReceiptResult? created = await createResponse.Content.ReadFromJsonAsync<ReceiptResult>();
        created.ShouldNotBeNull();

        HttpResponseMessage deleteResponse = await client.DeleteAsync($"/receipts/{created.Id}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Act
        HttpResponseMessage restoreResponse =
            await client.PostAsync($"/receipts/{created.Id}", new StringContent(""));

        // Assert
        restoreResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        ReceiptEntity? receipt = await _db.Receipts.FirstOrDefaultAsync(r => r.Id == created.Id);
        receipt.ShouldNotBeNull();
        receipt.DeletedAt.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_WithinEditWindow_UpdatesFields()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        using MultipartFormDataContent content = CreateReceiptFormData(amount: "10.00");
        HttpResponseMessage createResponse = await client.PostAsync("/receipts", content);
        ReceiptResult? created = await createResponse.Content.ReadFromJsonAsync<ReceiptResult>();
        created.ShouldNotBeNull();

        UpdateReceiptRequest update = new() { Amount = 42.50m, Vendor = "Corrected Vendor" };

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/receipts/{created.Id}", update);
        ReceiptResult? result = await response.Content.ReadFromJsonAsync<ReceiptResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Amount.ShouldBe(42.50m);
        result.Vendor.ShouldBe("Corrected Vendor");
    }

    [Fact]
    public async Task UpdateAsync_AfterEditWindow_ReturnsForbidden()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        using MultipartFormDataContent content = CreateReceiptFormData();
        HttpResponseMessage createResponse = await client.PostAsync("/receipts", content);
        ReceiptResult? created = await createResponse.Content.ReadFromJsonAsync<ReceiptResult>();
        created.ShouldNotBeNull();

        DateTimeOffset originalTime = Factory.FakeTime.GetUtcNow();
        try
        {
            Factory.FakeTime.SetUtcNow(originalTime.AddMinutes(16));

            // Act
            HttpResponseMessage response =
                await client.PatchAsJsonAsync($"/receipts/{created.Id}", new UpdateReceiptRequest { Amount = 5m });

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }
        finally
        {
            Factory.FakeTime.SetUtcNow(originalTime);
        }
    }

    [Fact]
    public async Task UpdateAsync_NotFound()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/receipts/{Guid.NewGuid()}", new UpdateReceiptRequest { Amount = 5m });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task UpdateAsync_PrivilegedNonOwner_ReturnsForbidden(UserRole role)
    {
        // Arrange
        (HttpClient otherClient, Guid _) = await CreateUserAndClientAsync(UserRole.USER, "receipt-owner");
        ReceiptResult receipt = await CreateReceiptAsync(otherClient, 10m, DateTime.UtcNow);

        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/receipts/{receipt.Id}", new UpdateReceiptRequest { Amount = 20m });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateAsync_NotOwnerNorPrivileged_ReturnsNotFound()
    {
        // Arrange
        UserEntity otherUser = new()
        {
            Id = Guid.NewGuid(),
            Username = "other-receipt-owner",
            PasswordHash = CryptoService.HashPassword("Test1234!"),
            Role = UserRole.USER
        };
        await _db.Users.AddAsync(otherUser);

        ReceiptEntity receipt = new()
        {
            Id = Guid.NewGuid(),
            UserId = otherUser.Id,
            Amount = 10,
            ReceiptDate = DateTime.UtcNow
        };
        await _db.Receipts.AddAsync(receipt);
        await _db.SaveChangesAsync();

        HttpClient client = CreateClient(UserRole.USER);

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/receipts/{receipt.Id}", new UpdateReceiptRequest { Amount = 5m });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------
    // GET /receipts
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetAllAsync_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/receipts");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, 0)]
    public async Task GetAllAsync_InvalidPaging_ReturnsBadRequest(int offset, int limit)
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.GetAsync($"/receipts?offset={offset}&limit={limit}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAllAsync_User_OnlySeesOwnReceipts()
    {
        // Arrange
        (HttpClient otherClient, Guid _) = await CreateUserAndClientAsync(UserRole.USER, "other-user-1");
        await CreateReceiptAsync(otherClient, 9m, DateTime.UtcNow);
        HttpClient client = CreateClient(UserRole.USER);
        ReceiptResult ownReceipt = await CreateReceiptAsync(client, 7m, DateTime.UtcNow);

        // Act
        HttpResponseMessage response = await client.GetAsync("/receipts");
        AllReceiptResults? result = await response.Content.ReadFromJsonAsync<AllReceiptResults>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Items.ShouldAllBe(r => r.UserId == UserId);
        result.Items.ShouldContain(r => r.Id == ownReceipt.Id);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetAllAsync_Privileged_SeesAllReceipts(UserRole role)
    {
        // Arrange
        (HttpClient otherClient, Guid _) = await CreateUserAndClientAsync(UserRole.USER, "other-user-2");
        ReceiptResult otherReceipt = await CreateReceiptAsync(otherClient, 9m, DateTime.UtcNow);
        HttpClient client = CreateClient(role);
        ReceiptResult ownReceipt = await CreateReceiptAsync(client, 7m, DateTime.UtcNow);

        // Act
        HttpResponseMessage response = await client.GetAsync("/receipts");
        AllReceiptResults? result = await response.Content.ReadFromJsonAsync<AllReceiptResults>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Items.ShouldContain(r => r.Id == ownReceipt.Id);
        result.Items.ShouldContain(r => r.Id == otherReceipt.Id);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByCategoryId()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        HttpResponseMessage categoryResponse =
            await client.PostAsJsonAsync("/receipt-categories", new ReceiptCategoryRequest { Name = "Fahrten" });
        ReceiptCategoryResult? category = await categoryResponse.Content.ReadFromJsonAsync<ReceiptCategoryResult>();
        category.ShouldNotBeNull();
        ReceiptResult categorized = await CreateReceiptAsync(client, 5m, DateTime.UtcNow, category.Id);
        ReceiptResult uncategorized = await CreateReceiptAsync(client, 6m, DateTime.UtcNow);

        // Act
        HttpResponseMessage response = await client.GetAsync($"/receipts?categoryId={category.Id}");
        AllReceiptResults? result = await response.Content.ReadFromJsonAsync<AllReceiptResults>();

        // Assert
        result.ShouldNotBeNull();
        result.Items.ShouldContain(r => r.Id == categorized.Id);
        result.Items.ShouldNotContain(r => r.Id == uncategorized.Id);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByDateRange()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        ReceiptResult inRange = await CreateReceiptAsync(client, 5m, new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc));
        ReceiptResult outOfRange = await CreateReceiptAsync(client, 6m, new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));

        // Act
        HttpResponseMessage response = await client.GetAsync(
            "/receipts?dateFrom=2026-03-01T00:00:00.000Z&dateTo=2026-03-31T23:59:59.000Z");
        AllReceiptResults? result = await response.Content.ReadFromJsonAsync<AllReceiptResults>();

        // Assert
        result.ShouldNotBeNull();
        result.Items.ShouldContain(r => r.Id == inRange.Id);
        result.Items.ShouldNotContain(r => r.Id == outOfRange.Id);
    }

    [Fact]
    public async Task GetAllAsync_Admin_DeletedFilter_ShowsOnlyDeleted()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        ReceiptResult active = await CreateReceiptAsync(client, 5m, DateTime.UtcNow);
        ReceiptResult deleted = await CreateReceiptAsync(client, 6m, DateTime.UtcNow);
        await client.DeleteAsync($"/receipts/{deleted.Id}");

        // Act
        HttpResponseMessage response = await client.GetAsync("/receipts?deleted=true");
        AllReceiptResults? result = await response.Content.ReadFromJsonAsync<AllReceiptResults>();

        // Assert
        result.ShouldNotBeNull();
        result.Items.ShouldContain(r => r.Id == deleted.Id);
        result.Items.ShouldNotContain(r => r.Id == active.Id);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetAllAsync_NonAdmin_DeletedFilterIgnored(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        ReceiptResult deleted = await CreateReceiptAsync(client, 6m, DateTime.UtcNow);
        await client.DeleteAsync($"/receipts/{deleted.Id}");

        // Act
        HttpResponseMessage response = await client.GetAsync("/receipts?deleted=true");
        AllReceiptResults? result = await response.Content.ReadFromJsonAsync<AllReceiptResults>();

        // Assert
        result.ShouldNotBeNull();
        result.Items.ShouldNotContain(r => r.Id == deleted.Id);
    }

    // ---------------------------------------------------------------
    // GET /receipts/{id}
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetAsync_Success()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        ReceiptResult created = await CreateReceiptAsync(client, 12.5m, DateTime.UtcNow);

        // Act
        HttpResponseMessage response = await client.GetAsync($"/receipts/{created.Id}");
        ReceiptResult? result = await response.Content.ReadFromJsonAsync<ReceiptResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Id.ShouldBe(created.Id);
    }

    [Fact]
    public async Task GetAsync_NotFound()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.GetAsync($"/receipts/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAsync_NotOwnerNorPrivileged_ReturnsNotFound()
    {
        // Arrange
        (HttpClient otherClient, Guid _) = await CreateUserAndClientAsync(UserRole.USER, "other-user");
        ReceiptResult otherReceipt = await CreateReceiptAsync(otherClient, 9m, DateTime.UtcNow);
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        HttpResponseMessage response = await client.GetAsync($"/receipts/{otherReceipt.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------
    // DELETE /receipts/{id} and /receipts/{id}/hard
    // ---------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_Success_Owner()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);
        ReceiptResult receipt = await CreateReceiptAsync(client, 5m, DateTime.UtcNow);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/receipts/{receipt.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ReceiptEntity? entity = await _db.Receipts.FirstOrDefaultAsync(r => r.Id == receipt.Id);
        entity.ShouldNotBeNull();
        entity.DeletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task DeleteAsync_NotFound()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/receipts/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_NotOwnerNorPrivileged_ReturnsNotFound()
    {
        // Arrange
        (HttpClient otherClient, Guid _) = await CreateUserAndClientAsync(UserRole.USER, "other-user");
        ReceiptResult otherReceipt = await CreateReceiptAsync(otherClient, 9m, DateTime.UtcNow);
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/receipts/{otherReceipt.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_Privileged_CanDeleteOthersReceipt()
    {
        // Arrange
        (HttpClient otherClient, Guid _) = await CreateUserAndClientAsync(UserRole.USER, "other-user");
        ReceiptResult otherReceipt = await CreateReceiptAsync(otherClient, 9m, DateTime.UtcNow);

        HttpClient client = CreateClient(UserRole.FINANCIAL_MANAGER);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/receipts/{otherReceipt.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HardDeleteAsync_Success_Admin()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        ReceiptResult receipt = await CreateReceiptAsync(client, 5m, DateTime.UtcNow);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/receipts/{receipt.Id}/hard");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ReceiptEntity? entity =
            await _db.Receipts.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == receipt.Id);
        entity.ShouldBeNull();
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task HardDeleteAsync_Forbidden_WhenNotAdmin(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        ReceiptResult receipt = await CreateReceiptAsync(client, 5m, DateTime.UtcNow);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/receipts/{receipt.Id}/hard");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---------------------------------------------------------------
    // POST /receipts - validation
    // ---------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_AmountNotPositive_ReturnsBadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        using MultipartFormDataContent content = CreateReceiptFormData(amount: "0.00");

        // Act
        HttpResponseMessage response = await client.PostAsync("/receipts", content);
        ErrorDetailsResult? result = await response.Content.ReadFromJsonAsync<ErrorDetailsResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.ShouldNotBeNull();
        result.MoreInfo.ShouldBe("Amount must be greater than 0.");
    }

    [Fact]
    public async Task CreateAsync_MissingReceiptDate_ReturnsBadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        using MultipartFormDataContent content = new() { { new StringContent("10.00"), "amount" } };

        // Act
        HttpResponseMessage response = await client.PostAsync("/receipts", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAsync_InvalidCategory_ReturnsBadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        using MultipartFormDataContent content = CreateReceiptFormData();
        content.Add(new StringContent(Guid.NewGuid().ToString()), "categoryId");

        // Act
        HttpResponseMessage response = await client.PostAsync("/receipts", content);
        ErrorDetailsResult? result = await response.Content.ReadFromJsonAsync<ErrorDetailsResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.ShouldNotBeNull();
        result.MoreInfo.ShouldBe("Category not found.");
    }

    // ---------------------------------------------------------------
    // PATCH /receipts/{id} - additional coverage
    // ---------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_AmountNotPositive_ReturnsBadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        ReceiptResult receipt = await CreateReceiptAsync(client, 10m, DateTime.UtcNow);

        // Act
        HttpResponseMessage response =
            await client.PatchAsJsonAsync($"/receipts/{receipt.Id}", new UpdateReceiptRequest { Amount = 0m });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateAsync_InvalidCategory_ReturnsBadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        ReceiptResult receipt = await CreateReceiptAsync(client, 10m, DateTime.UtcNow);

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/receipts/{receipt.Id}",
            new UpdateReceiptRequest { CategoryId = Guid.NewGuid() });
        ErrorDetailsResult? result = await response.Content.ReadFromJsonAsync<ErrorDetailsResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.ShouldNotBeNull();
        result.MoreInfo.ShouldBe("Category not found.");
    }

    [Fact]
    public async Task UpdateAsync_AlreadyDeletedReceipt_ReturnsNotFound()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        ReceiptResult receipt = await CreateReceiptAsync(client, 10m, DateTime.UtcNow);
        await client.DeleteAsync($"/receipts/{receipt.Id}");

        // Act
        HttpResponseMessage response =
            await client.PatchAsJsonAsync($"/receipts/{receipt.Id}", new UpdateReceiptRequest { Amount = 20m });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateAsync_PartialUpdate_OnlyChangesProvidedFields()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        using MultipartFormDataContent content =
            CreateReceiptFormData(amount: "10.00", fileName: "receipt.pdf", contentType: "application/pdf");
        content.Add(new StringContent("Original Vendor"), "vendor");
        HttpResponseMessage createResponse = await client.PostAsync("/receipts", content);
        ReceiptResult? receipt = await createResponse.Content.ReadFromJsonAsync<ReceiptResult>();
        receipt.ShouldNotBeNull();

        // Act
        HttpResponseMessage response =
            await client.PatchAsJsonAsync($"/receipts/{receipt.Id}", new UpdateReceiptRequest { Amount = 99m });
        ReceiptResult? result = await response.Content.ReadFromJsonAsync<ReceiptResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Amount.ShouldBe(99m);
        result.Vendor.ShouldBe("Original Vendor");
    }

    // ---------------------------------------------------------------
    // GET /receipts/analytics
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(UserRole.USER)]
    public async Task GetAnalyticsAsync_Forbidden_WhenNotPrivileged(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.GetAsync("/receipts/analytics");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAnalyticsAsync_DefaultsToCurrentYear()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.GetAsync("/receipts/analytics");
        ReceiptAnalyticsResult? result = await response.Content.ReadFromJsonAsync<ReceiptAnalyticsResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Year.ShouldBe(DateTime.UtcNow.Year);
    }

    [Fact]
    public async Task GetAnalyticsAsync_ByCategory_OnlyIncludesSelectedYear()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        ReceiptCategoryEntity category = await CreateReceiptCategoryAsync("Travel expenses");

        await CreateReceiptAsync(client, 100m, new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc), category.Id);
        await CreateReceiptAsync(client, 10m, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), category.Id);

        // Act
        HttpResponseMessage response = await client.GetAsync("/receipts/analytics?year=2026");
        ReceiptAnalyticsResult? result = await response.Content.ReadFromJsonAsync<ReceiptAnalyticsResult>();

        // Assert
        result.ShouldNotBeNull();
        ReceiptCategoryTotal categoryTotal = result.ByCategory.ShouldHaveSingleItem();
        categoryTotal.Total.ShouldBe(10m);
    }

    [Fact]
    public async Task GetAnalyticsAsync_ByMonth_IncludesPerCategoryBreakdown()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        ReceiptCategoryEntity categoryA = await CreateReceiptCategoryAsync("Eat");
        ReceiptCategoryEntity categoryB = await CreateReceiptCategoryAsync("Travel expenses");

        await CreateReceiptAsync(client, 30m, new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc), categoryA.Id);
        await CreateReceiptAsync(client, 20m, new DateTime(2026, 4, 12, 0, 0, 0, DateTimeKind.Utc), categoryB.Id);

        // Act
        HttpResponseMessage response = await client.GetAsync("/receipts/analytics?year=2026");
        ReceiptAnalyticsResult? result = await response.Content.ReadFromJsonAsync<ReceiptAnalyticsResult>();

        // Assert
        result.ShouldNotBeNull();
        ReceiptMonthlyTotal april = result.ByMonth.ShouldHaveSingleItem();
        april.Month.ShouldBe(4);
        april.Total.ShouldBe(50m);
        april.ByCategory.Count.ShouldBe(2);
        april.ByCategory.ShouldContain(c => c.CategoryId == categoryA.Id && c.Total == 30m);
        april.ByCategory.ShouldContain(c => c.CategoryId == categoryB.Id && c.Total == 20m);
    }

    [Fact]
    public async Task GetAnalyticsAsync_ByYear_IncludesPerCategoryBreakdown_AcrossAllYears()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        ReceiptCategoryEntity category = await CreateReceiptCategoryAsync("Eat");

        await CreateReceiptAsync(client, 15m, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), category.Id);
        await CreateReceiptAsync(client, 25m, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), category.Id);

        // Act
        HttpResponseMessage response = await client.GetAsync("/receipts/analytics?year=2026");
        ReceiptAnalyticsResult? result = await response.Content.ReadFromJsonAsync<ReceiptAnalyticsResult>();

        // Assert
        result.ShouldNotBeNull();
        result.ByYear.Count.ShouldBe(2);

        ReceiptYearlyTotal year2025 = result.ByYear.Single(y => y.Year == 2025);
        year2025.Total.ShouldBe(15m);
        year2025.ByCategory.ShouldHaveSingleItem().Total.ShouldBe(15m);

        ReceiptYearlyTotal year2026 = result.ByYear.Single(y => y.Year == 2026);
        year2026.Total.ShouldBe(25m);
        year2026.ByCategory.ShouldHaveSingleItem().Total.ShouldBe(25m);
    }

    private async Task<ReceiptCategoryEntity> CreateReceiptCategoryAsync(string name)
    {
        ReceiptCategoryEntity category = new()
        {
            Id = Guid.NewGuid(),
            Name = name
        };

        await _db.ReceiptCategories.AddAsync(category);
        await _db.SaveChangesAsync();

        return category;
    }

    // ---------------------------------------------------------------
    // Payment method restrictions (ADMIN/FINANCIAL_MANAGER only)
    // ---------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_UserSetsPaymentMethod_ReturnsForbidden()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);
        using MultipartFormDataContent content = CreateReceiptFormData();
        content.Add(new StringContent(nameof(ReceiptPaymentMethod.CASH)), "paymentMethod");

        // Act
        HttpResponseMessage response = await client.PostAsync("/receipts", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task CreateAsync_PrivilegedSetsPaymentMethod_Success(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        using MultipartFormDataContent content = CreateReceiptFormData();
        content.Add(new StringContent(nameof(ReceiptPaymentMethod.CASH)), "paymentMethod");

        // Act
        HttpResponseMessage response = await client.PostAsync("/receipts", content);
        ReceiptResult? result = await response.Content.ReadFromJsonAsync<ReceiptResult>(JsonOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        result.ShouldNotBeNull();
        result.PaymentMethod.ShouldBe(ReceiptPaymentMethod.CASH);
    }

    [Fact]
    public async Task UpdateAsync_UserSetsPaymentMethod_ReturnsForbidden()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);
        ReceiptResult receipt = await CreateReceiptAsync(client, 10m, DateTime.UtcNow);

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/receipts/{receipt.Id}",
            new UpdateReceiptRequest { PaymentMethod = ReceiptPaymentMethod.CARD });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateAsync_UserUpdatesOtherFieldsWithoutTouchingPaymentMethod_Success()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);
        ReceiptResult receipt = await CreateReceiptAsync(client, 10m, DateTime.UtcNow);

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/receipts/{receipt.Id}",
            new UpdateReceiptRequest { Vendor = "New Vendor" });
        ReceiptResult? result = await response.Content.ReadFromJsonAsync<ReceiptResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Vendor.ShouldBe("New Vendor");
    }

    [Fact]
    public async Task UpdateAsync_PrivilegedSetsPaymentMethod_Success()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.FINANCIAL_MANAGER);
        ReceiptResult receipt = await CreateReceiptAsync(client, 10m, DateTime.UtcNow);

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/receipts/{receipt.Id}",
            new UpdateReceiptRequest { PaymentMethod = ReceiptPaymentMethod.BANK_TRANSFER });
        ReceiptResult? result = await response.Content.ReadFromJsonAsync<ReceiptResult>(JsonOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.PaymentMethod.ShouldBe(ReceiptPaymentMethod.BANK_TRANSFER);
    }

    // ---------------------------------------------------------------
    // POST /receipts/{id}/pay
    // ---------------------------------------------------------------

    [Fact]
    public async Task MarkAsPaidAsync_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response =
            await client.PostAsJsonAsync($"/receipts/{Guid.NewGuid()}/pay", new MarkReceiptPaidRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MarkAsPaidAsync_AsUser_ReturnsForbidden()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);
        ReceiptResult receipt = await CreateReceiptAsync(client, 10m, DateTime.UtcNow);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/receipts/{receipt.Id}/pay",
            new MarkReceiptPaidRequest { PaymentMethod = ReceiptPaymentMethod.CASH });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MarkAsPaidAsync_NotFound()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/receipts/{Guid.NewGuid()}/pay",
            new MarkReceiptPaidRequest { PaymentMethod = ReceiptPaymentMethod.CASH });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MarkAsPaidAsync_WithoutPaymentMethodAnywhere_ReturnsBadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        ReceiptResult receipt = await CreateReceiptAsync(client, 10m, DateTime.UtcNow);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/receipts/{receipt.Id}/pay",
            new MarkReceiptPaidRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MarkAsPaidAsync_PaymentMethodAlreadySet_DoesNotRequireBodyValue()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.FINANCIAL_MANAGER);
        using MultipartFormDataContent content = CreateReceiptFormData();
        content.Add(new StringContent(nameof(ReceiptPaymentMethod.PAYPAL)), "paymentMethod");
        HttpResponseMessage createResponse = await client.PostAsync("/receipts", content);
        ReceiptResult? receipt = await createResponse.Content.ReadFromJsonAsync<ReceiptResult>(JsonOptions);
        receipt.ShouldNotBeNull();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/receipts/{receipt.Id}/pay",
            new MarkReceiptPaidRequest());
        ReceiptResult? updated =
            await client.GetFromJsonAsync<ReceiptResult>($"/receipts/{receipt.Id}", JsonOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        updated.ShouldNotBeNull();
        updated.Paid.ShouldBeTrue();
        updated.PaymentMethod.ShouldBe(ReceiptPaymentMethod.PAYPAL);
    }

    [Fact]
    public async Task MarkAsPaidAsync_FillsInPaymentMethod_WhenNotYetSet()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        ReceiptResult receipt = await CreateReceiptAsync(client, 10m, DateTime.UtcNow);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/receipts/{receipt.Id}/pay",
            new MarkReceiptPaidRequest { PaymentMethod = ReceiptPaymentMethod.SEPA_DIRECT_DEBIT });
        ReceiptResult? updated =
            await client.GetFromJsonAsync<ReceiptResult>($"/receipts/{receipt.Id}", JsonOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        updated.ShouldNotBeNull();
        updated.Paid.ShouldBeTrue();
        updated.PaymentMethod.ShouldBe(ReceiptPaymentMethod.SEPA_DIRECT_DEBIT);
    }

    [Fact]
    public async Task MarkAsPaidAsync_AlreadyPaid_ReturnsConflict()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        ReceiptResult receipt = await CreateReceiptAsync(client, 10m, DateTime.UtcNow);
        await client.PostAsJsonAsync($"/receipts/{receipt.Id}/pay",
            new MarkReceiptPaidRequest { PaymentMethod = ReceiptPaymentMethod.CASH });

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/receipts/{receipt.Id}/pay",
            new MarkReceiptPaidRequest { PaymentMethod = ReceiptPaymentMethod.CARD });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task MarkAsPaidAsync_DeletedReceipt_ReturnsNotFound()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        ReceiptResult receipt = await CreateReceiptAsync(client, 10m, DateTime.UtcNow);
        await client.DeleteAsync($"/receipts/{receipt.Id}");

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/receipts/{receipt.Id}/pay",
            new MarkReceiptPaidRequest { PaymentMethod = ReceiptPaymentMethod.CASH });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateAsync_PaidReceipt_ReturnsForbidden()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        ReceiptResult receipt = await CreateReceiptAsync(client, 10m, DateTime.UtcNow);
        await client.PostAsJsonAsync($"/receipts/{receipt.Id}/pay",
            new MarkReceiptPaidRequest { PaymentMethod = ReceiptPaymentMethod.CASH });

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/receipts/{receipt.Id}",
            new UpdateReceiptRequest { Vendor = "Should not apply" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByPaid()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        ReceiptResult open = await CreateReceiptAsync(client, 5m, DateTime.UtcNow);
        ReceiptResult paid = await CreateReceiptAsync(client, 6m, DateTime.UtcNow);
        await client.PostAsJsonAsync($"/receipts/{paid.Id}/pay",
            new MarkReceiptPaidRequest { PaymentMethod = ReceiptPaymentMethod.CASH });

        // Act
        HttpResponseMessage response = await client.GetAsync("/receipts?paid=true");
        AllReceiptResults? result = await response.Content.ReadFromJsonAsync<AllReceiptResults>(JsonOptions);

        // Assert
        result.ShouldNotBeNull();
        result.Items.ShouldContain(r => r.Id == paid.Id);
        result.Items.ShouldNotContain(r => r.Id == open.Id);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByPaid_False_ShowsOnlyOpen()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        ReceiptResult open = await CreateReceiptAsync(client, 5m, DateTime.UtcNow);
        ReceiptResult paid = await CreateReceiptAsync(client, 6m, DateTime.UtcNow);
        await client.PostAsJsonAsync($"/receipts/{paid.Id}/pay",
            new MarkReceiptPaidRequest { PaymentMethod = ReceiptPaymentMethod.CASH });

        // Act
        HttpResponseMessage response = await client.GetAsync("/receipts?paid=false");
        AllReceiptResults? result = await response.Content.ReadFromJsonAsync<AllReceiptResults>();

        // Assert
        result.ShouldNotBeNull();
        result.Items.ShouldContain(r => r.Id == open.Id);
        result.Items.ShouldNotContain(r => r.Id == paid.Id);
    }

    [Fact]
    public async Task CreateAsync_Success_DefaultsToUnpaid()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        ReceiptResult receipt = await CreateReceiptAsync(client, 10m, DateTime.UtcNow);

        // Assert
        receipt.Paid.ShouldBeFalse();
    }

    [Fact]
    public async Task MarkAsPaidAsync_WritesAuditLogEntry()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        ReceiptResult receipt = await CreateReceiptAsync(client, 10m, DateTime.UtcNow);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/receipts/{receipt.Id}/pay",
            new MarkReceiptPaidRequest { PaymentMethod = ReceiptPaymentMethod.CASH });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        AuditLogEntity? auditLog = await _db.AuditLogs.FirstOrDefaultAsync(l =>
            l.Data.Contains(receipt.Id.ToString()) && l.Action == nameof(AuditLogActions.UPDATE));
        auditLog.ShouldNotBeNull();
        auditLog.Entity.ShouldBe(nameof(ReceiptEntity));
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task MarkAsPaidAsync_PrivilegedMarksOtherUsersReceipt_Success(UserRole role)
    {
        // Arrange
        (HttpClient otherClient, Guid _) = await CreateUserAndClientAsync(UserRole.USER, "receipt-owner-pay");
        ReceiptResult receipt = await CreateReceiptAsync(otherClient, 10m, DateTime.UtcNow);

        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/receipts/{receipt.Id}/pay",
            new MarkReceiptPaidRequest { PaymentMethod = ReceiptPaymentMethod.CASH });
        ReceiptResult? updated =
            await client.GetFromJsonAsync<ReceiptResult>($"/receipts/{receipt.Id}", JsonOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        updated.ShouldNotBeNull();
        updated.Paid.ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteAsync_PaidReceipt_StillSucceeds()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        ReceiptResult receipt = await CreateReceiptAsync(client, 10m, DateTime.UtcNow);
        await client.PostAsJsonAsync($"/receipts/{receipt.Id}/pay",
            new MarkReceiptPaidRequest { PaymentMethod = ReceiptPaymentMethod.CASH });

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/receipts/{receipt.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ReceiptEntity? entity = await _db.Receipts.FirstOrDefaultAsync(r => r.Id == receipt.Id);
        entity.ShouldNotBeNull();
        entity.DeletedAt.ShouldNotBeNull();
    }
}
