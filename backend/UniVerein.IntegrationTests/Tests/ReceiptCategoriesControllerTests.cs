using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.ApiResults.Receipt;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Services;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Shouldly;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class ReceiptCategoriesControllerTests : IntegrationTestBase
{
    private readonly AppDbContext _db;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ReceiptCategoriesControllerTests(UniVereinWebApplicationFactory factory)
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
    }

    // ---------------------------------------------------------------
    // GET /receipt-categories
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetAllAsync_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/receipt-categories");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    [InlineData(UserRole.ADMIN)]
    public async Task GetAllAsync_AnyAuthenticatedRole_ReturnsOrderedCategories(UserRole role)
    {
        // Arrange
        await _db.ReceiptCategories.AddRangeAsync(
            new ReceiptCategoryEntity { Id = Guid.NewGuid(), Name = "Eat" },
            new ReceiptCategoryEntity { Id = Guid.NewGuid(), Name = "Party" });
        await _db.SaveChangesAsync();
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.GetAsync("/receipt-categories");
        ReceiptCategoryResults? result = await response.Content.ReadFromJsonAsync<ReceiptCategoryResults>(JsonOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Total.ShouldBe(2);
        result.Items.Select(i => i.Name).ShouldBe(new[] { "Eat", "Party" });
    }

    // ---------------------------------------------------------------
    // POST /receipt-categories
    // ---------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_Forbidden_WhenUser()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        HttpResponseMessage response =
            await client.PostAsJsonAsync("/receipt-categories", new ReceiptCategoryRequest { Name = "Eat" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task CreateAsync_Success(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response =
            await client.PostAsJsonAsync("/receipt-categories", new ReceiptCategoryRequest { Name = "Eat" });
        ReceiptCategoryResult? result = await response.Content.ReadFromJsonAsync<ReceiptCategoryResult>(JsonOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Eat");
    }

    [Fact]
    public async Task CreateAsync_EmptyName_ReturnsBadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response =
            await client.PostAsJsonAsync("/receipt-categories", new ReceiptCategoryRequest { Name = "   " });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAsync_NameTooLong_ReturnsBadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        string tooLong = new('a', 51);

        // Act
        HttpResponseMessage response =
            await client.PostAsJsonAsync("/receipt-categories", new ReceiptCategoryRequest { Name = tooLong });
        ErrorDetailsResult? result = await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(JsonOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.ShouldNotBeNull();
        result.MoreInfo.ShouldBe("Name length must be less long then 51 characters.");
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_ReturnsConflict()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        await CreateReceiptCategoryAsync("Eat");

        // Act
        HttpResponseMessage response =
            await client.PostAsJsonAsync("/receipt-categories", new ReceiptCategoryRequest { Name = "Eat" });
        ErrorDetailsResult? result = await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(JsonOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        result.ShouldNotBeNull();
        result.ErrorCode.ShouldBe(ApiErrorCodes.CONFLICT_RESOURCE_ALREADY_EXISTS);
    }

    [Fact]
    public async Task CreateAsync_SameNameAsSoftDeletedCategory_RestoresIt()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        ReceiptCategoryEntity category = await CreateReceiptCategoryAsync("Eat", true);

        // Act
        HttpResponseMessage response =
            await client.PostAsJsonAsync("/receipt-categories", new ReceiptCategoryRequest { Name = "Eat" });
        ReceiptCategoryResult? result = await response.Content.ReadFromJsonAsync<ReceiptCategoryResult>(JsonOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        result.ShouldNotBeNull();
        result.Id.ShouldBe(category.Id);
        ReceiptCategoryEntity? entity = await _db.ReceiptCategories.FirstOrDefaultAsync(c => c.Id == category.Id);
        entity.ShouldNotBeNull();
        entity.DeletedAt.ShouldBeNull();
    }

    // ---------------------------------------------------------------
    // DELETE /receipt-categories/{id}
    // ---------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_Forbidden_WhenUser()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/receipt-categories/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteAsync_NotFound()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/receipt-categories/{Guid.NewGuid()}");
        ErrorDetailsResult? result = await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(JsonOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result.ErrorCode.ShouldBe(ApiErrorCodes.RESOURCE_NOT_FOUND);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task DeleteAsync_Success_WhenUnused(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        ReceiptCategoryEntity category = await CreateReceiptCategoryAsync("Eat");

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/receipt-categories/{category.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ReceiptCategoryEntity? entity =
            await _db.ReceiptCategories.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == category.Id);
        entity.ShouldNotBeNull();
        entity.DeletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task DeleteAsync_BlockedWhenAssignedToActiveReceipt()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        ReceiptCategoryEntity category = await CreateReceiptCategoryAsync("Eat");

        await _db.Receipts.AddAsync(new ReceiptEntity
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            CategoryId = category.Id,
            Amount = 10,
            ReceiptDate = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/receipt-categories/{category.Id}");
        ErrorDetailsResult? result = await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(JsonOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.ShouldNotBeNull();
        result.MoreInfo.ShouldBe("Receipt category could not be deleted because it is assigned to a receipt.");
        ReceiptCategoryEntity? entity = await _db.ReceiptCategories.FirstOrDefaultAsync(c => c.Id == category.Id);
        entity.ShouldNotBeNull();
        entity.DeletedAt.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsync_AllowedWhenOnlyAssignedToSoftDeletedReceipt()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        ReceiptCategoryEntity category = await CreateReceiptCategoryAsync("Eat");

        ReceiptEntity receipt = new()
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            CategoryId = category.Id,
            Amount = 10,
            ReceiptDate = DateTime.UtcNow,
            DeletedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        await _db.Receipts.AddAsync(receipt);
        await _db.SaveChangesAsync();

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/receipt-categories/{category.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<ReceiptCategoryEntity> CreateReceiptCategoryAsync(string name, bool? deleted = null)
    {
        ReceiptCategoryEntity category = new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            DeletedAt = deleted == true ? DateTimeOffset.UtcNow.AddDays(-1) : null
        };

        await WithDbContext(async db =>
        {
            await db.ReceiptCategories.AddAsync(category);
            await db.SaveChangesAsync();
        });

        return category;
    }

    [Fact]
    public async Task CreateAsync_WithTaxArea_StoresTaxArea()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/receipt-categories",
            new ReceiptCategoryRequest { Name = "Kiosk", TaxArea = TaxArea.COMMERCIAL_OPERATION }, JsonOptions);
        ReceiptCategoryResult? result = await response.Content.ReadFromJsonAsync<ReceiptCategoryResult>(JsonOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        result.ShouldNotBeNull();
        result.TaxArea.ShouldBe(TaxArea.COMMERCIAL_OPERATION);
        (await _db.ReceiptCategories.SingleAsync()).TaxArea.ShouldBe(TaxArea.COMMERCIAL_OPERATION);
    }

    [Fact]
    public async Task CreateAsync_WithoutTaxArea_DefaultsToIdeell()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/receipt-categories", new ReceiptCategoryRequest { Name = "Kiosk" });
        ReceiptCategoryResult? result = await response.Content.ReadFromJsonAsync<ReceiptCategoryResult>(JsonOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        result.ShouldNotBeNull();
        result.TaxArea.ShouldBe(TaxArea.NON_PROFIT);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task UpdateAsync_ChangesNameAndTaxArea(UserRole role)
    {
        // Arrange
        ReceiptCategoryEntity category = new() { Id = Guid.NewGuid(), Name = "Kiosk" };
        await _db.ReceiptCategories.AddAsync(category);
        await _db.SaveChangesAsync();
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync($"/receipt-categories/{category.Id}",
            new ReceiptCategoryRequest { Name = " Fest ", TaxArea = TaxArea.PURPOSE_RELATED_OPERATION }, JsonOptions);
        ReceiptCategoryResult? result = await response.Content.ReadFromJsonAsync<ReceiptCategoryResult>(JsonOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Fest");
        result.TaxArea.ShouldBe(TaxArea.PURPOSE_RELATED_OPERATION);
    }

    [Fact]
    public async Task UpdateAsync_WithoutTaxArea_KeepsTaxArea()
    {
        // Arrange
        ReceiptCategoryEntity category = new() { Id = Guid.NewGuid(), Name = "Kiosk", TaxArea = TaxArea.PURPOSE_RELATED_OPERATION };
        await _db.ReceiptCategories.AddAsync(category);
        await _db.SaveChangesAsync();
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync($"/receipt-categories/{category.Id}", new ReceiptCategoryRequest { Name = "Fest" });
        ReceiptCategoryResult? result = await response.Content.ReadFromJsonAsync<ReceiptCategoryResult>(JsonOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.TaxArea.ShouldBe(TaxArea.PURPOSE_RELATED_OPERATION);
    }

    [Fact]
    public async Task UpdateAsync_DuplicateName_ReturnsConflict()
    {
        // Arrange
        ReceiptCategoryEntity first = new() { Id = Guid.NewGuid(), Name = "Kiosk" };
        ReceiptCategoryEntity second = new() { Id = Guid.NewGuid(), Name = "Fest" };
        await _db.ReceiptCategories.AddRangeAsync(first, second);
        await _db.SaveChangesAsync();
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync($"/receipt-categories/{second.Id}", new ReceiptCategoryRequest { Name = "Kiosk" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateAsync_UnknownCategory_ReturnsNotFound()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync($"/receipt-categories/{Guid.NewGuid()}", new ReceiptCategoryRequest { Name = "Fest" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateAsync_User_ReturnsForbidden()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync($"/receipt-categories/{Guid.NewGuid()}", new ReceiptCategoryRequest { Name = "Fest" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
