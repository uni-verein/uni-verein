using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults.Income;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Shouldly;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class IncomeCategoriesControllerTests : IntegrationTestBase
{
    private readonly AppDbContext _db;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public IncomeCategoriesControllerTests(UniVereinWebApplicationFactory factory)
        : base(factory)
    {
        _db = GetService<AppDbContext>();
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _db.Incomes.RemoveRange(_db.Incomes.IgnoreQueryFilters());
        _db.IncomeCategories.RemoveRange(_db.IncomeCategories.IgnoreQueryFilters());
        await _db.ForceSaveChangesAsync();
    }

    // ---------------------------------------------------------------
    // GET /income-categories
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetAllAsync_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/income-categories");

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
        await _db.IncomeCategories.AddRangeAsync(
            new IncomeCategoryEntity { Id = Guid.NewGuid(), Name = "Sponsoring" },
            new IncomeCategoryEntity { Id = Guid.NewGuid(), Name = "Feste" });
        await _db.SaveChangesAsync();
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.GetAsync("/income-categories");
        IncomeCategoryResults? result = await response.Content.ReadFromJsonAsync<IncomeCategoryResults>(JsonOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Total.ShouldBe(2);
        result.Items.Select(i => i.Name).ShouldBe(new[] { "Feste", "Sponsoring" });
    }

    // ---------------------------------------------------------------
    // POST /income-categories
    // ---------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_Forbidden_WhenUser()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        HttpResponseMessage response =
            await client.PostAsJsonAsync("/income-categories", new IncomeCategoryRequest { Name = "Feste" });

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
            await client.PostAsJsonAsync("/income-categories", new IncomeCategoryRequest { Name = " Feste " });
        IncomeCategoryResult? result = await response.Content.ReadFromJsonAsync<IncomeCategoryResult>(JsonOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Feste");
    }

    [Fact]
    public async Task CreateAsync_EmptyName_ReturnsBadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response =
            await client.PostAsJsonAsync("/income-categories", new IncomeCategoryRequest { Name = "   " });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAsync_NameTooLong_ReturnsBadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response =
            await client.PostAsJsonAsync("/income-categories", new IncomeCategoryRequest { Name = new string('a', 51) });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_ReturnsConflict()
    {
        // Arrange
        await _db.IncomeCategories.AddAsync(new IncomeCategoryEntity { Id = Guid.NewGuid(), Name = "Feste" });
        await _db.SaveChangesAsync();
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response =
            await client.PostAsJsonAsync("/income-categories", new IncomeCategoryRequest { Name = "Feste" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateAsync_SoftDeletedName_RestoresCategory()
    {
        // Arrange
        IncomeCategoryEntity category = new() { Id = Guid.NewGuid(), Name = "Feste" };
        await _db.IncomeCategories.AddAsync(category);
        await _db.SaveChangesAsync();
        _db.IncomeCategories.Remove(category);
        await _db.SaveChangesAsync();
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response =
            await client.PostAsJsonAsync("/income-categories", new IncomeCategoryRequest { Name = "Feste" });
        IncomeCategoryResult? result = await response.Content.ReadFromJsonAsync<IncomeCategoryResult>(JsonOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        result.ShouldNotBeNull();
        result.Id.ShouldBe(category.Id);
        (await _db.IncomeCategories.CountAsync()).ShouldBe(1);
    }

    // ---------------------------------------------------------------
    // DELETE /income-categories/{id}
    // ---------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_Forbidden_WhenUser()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/income-categories/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteAsync_NotFound()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/income-categories/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task DeleteAsync_Success_SoftDeletesCategory(UserRole role)
    {
        // Arrange
        IncomeCategoryEntity category = new() { Id = Guid.NewGuid(), Name = "Feste" };
        await _db.IncomeCategories.AddAsync(category);
        await _db.SaveChangesAsync();
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/income-categories/{category.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _db.IncomeCategories.CountAsync()).ShouldBe(0);
        (await _db.IncomeCategories.IgnoreQueryFilters().CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task DeleteAsync_CategoryInUse_ReturnsBadRequest()
    {
        // Arrange
        IncomeCategoryEntity category = new() { Id = Guid.NewGuid(), Name = "Feste" };
        await _db.IncomeCategories.AddAsync(category);
        await _db.Incomes.AddAsync(new IncomeEntity
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Payer = "Sommerfest",
            Amount = 100,
            IncomeDate = DateTime.UtcNow.Date
        });
        await _db.SaveChangesAsync();
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/income-categories/{category.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _db.IncomeCategories.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task CreateAsync_WithTaxArea_StoresTaxArea()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/income-categories",
            new IncomeCategoryRequest { Name = "Kiosk", TaxArea = TaxArea.COMMERCIAL_OPERATION }, JsonOptions);
        IncomeCategoryResult? result = await response.Content.ReadFromJsonAsync<IncomeCategoryResult>(JsonOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        result.ShouldNotBeNull();
        result.TaxArea.ShouldBe(TaxArea.COMMERCIAL_OPERATION);
        (await _db.IncomeCategories.SingleAsync()).TaxArea.ShouldBe(TaxArea.COMMERCIAL_OPERATION);
    }

    [Fact]
    public async Task CreateAsync_WithoutTaxArea_DefaultsToIdeell()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/income-categories", new IncomeCategoryRequest { Name = "Kiosk" });
        IncomeCategoryResult? result = await response.Content.ReadFromJsonAsync<IncomeCategoryResult>(JsonOptions);

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
        IncomeCategoryEntity category = new() { Id = Guid.NewGuid(), Name = "Kiosk" };
        await _db.IncomeCategories.AddAsync(category);
        await _db.SaveChangesAsync();
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync($"/income-categories/{category.Id}",
            new IncomeCategoryRequest { Name = " Fest ", TaxArea = TaxArea.PURPOSE_RELATED_OPERATION }, JsonOptions);
        IncomeCategoryResult? result = await response.Content.ReadFromJsonAsync<IncomeCategoryResult>(JsonOptions);

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
        IncomeCategoryEntity category = new() { Id = Guid.NewGuid(), Name = "Kiosk", TaxArea = TaxArea.PURPOSE_RELATED_OPERATION };
        await _db.IncomeCategories.AddAsync(category);
        await _db.SaveChangesAsync();
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync($"/income-categories/{category.Id}", new IncomeCategoryRequest { Name = "Fest" });
        IncomeCategoryResult? result = await response.Content.ReadFromJsonAsync<IncomeCategoryResult>(JsonOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.TaxArea.ShouldBe(TaxArea.PURPOSE_RELATED_OPERATION);
    }

    [Fact]
    public async Task UpdateAsync_DuplicateName_ReturnsConflict()
    {
        // Arrange
        IncomeCategoryEntity first = new() { Id = Guid.NewGuid(), Name = "Kiosk" };
        IncomeCategoryEntity second = new() { Id = Guid.NewGuid(), Name = "Fest" };
        await _db.IncomeCategories.AddRangeAsync(first, second);
        await _db.SaveChangesAsync();
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync($"/income-categories/{second.Id}", new IncomeCategoryRequest { Name = "Kiosk" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateAsync_UnknownCategory_ReturnsNotFound()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync($"/income-categories/{Guid.NewGuid()}", new IncomeCategoryRequest { Name = "Fest" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateAsync_User_ReturnsForbidden()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync($"/income-categories/{Guid.NewGuid()}", new IncomeCategoryRequest { Name = "Fest" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
