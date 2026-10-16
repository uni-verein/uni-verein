using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults.Calendar;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class CalendarCategoriesControllerTests : IntegrationTestBase
{
    public CalendarCategoriesControllerTests(UniVereinWebApplicationFactory factory) : base(factory)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await WithDbContext(async db =>
        {
            db.CalendarEventParticipants.RemoveRange(db.CalendarEventParticipants.IgnoreQueryFilters());
            db.CalendarEvents.RemoveRange(db.CalendarEvents.IgnoreQueryFilters());
            db.CalendarCategories.RemoveRange(db.CalendarCategories.IgnoreQueryFilters());
            await db.ForceSaveChangesAsync();
        });
    }

    [Fact]
    public async Task GetAllAsync_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/calendar/categories");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task CreateAsync_Forbidden_WhenNotAdmin(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/calendar/categories",
            new CalendarCategoryRequest { Name = "Training", Color = "#2563eb" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateAsync_Admin_CategoryIsListedForUsers()
    {
        // Arrange
        HttpClient admin = CreateClient(UserRole.ADMIN);
        HttpClient user = CreateClient(UserRole.USER);

        // Act
        HttpResponseMessage response = await admin.PostAsJsonAsync("/calendar/categories",
            new CalendarCategoryRequest { Name = "Training", Color = "#2563eb" });
        CalendarCategoryResults? result = await user.GetFromJsonAsync<CalendarCategoryResults>("/calendar/categories");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        result!.Items.ShouldHaveSingleItem();
        result.Items[0].Name.ShouldBe("Training");
        result.Items[0].Color.ShouldBe("#2563eb");
    }

    [Theory]
    [InlineData("blue")]
    [InlineData("#12345")]
    [InlineData("#gggggg")]
    public async Task CreateAsync_InvalidColor_ReturnsBadRequest(string color)
    {
        // Arrange
        HttpClient admin = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await admin.PostAsJsonAsync("/calendar/categories",
            new CalendarCategoryRequest { Name = "Training", Color = color });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_ReturnsConflict()
    {
        // Arrange
        HttpClient admin = CreateClient(UserRole.ADMIN);
        await admin.PostAsJsonAsync("/calendar/categories",
            new CalendarCategoryRequest { Name = "Training", Color = "#2563eb" });

        // Act
        HttpResponseMessage response = await admin.PostAsJsonAsync("/calendar/categories",
            new CalendarCategoryRequest { Name = "Training", Color = "#ff0000" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateAsync_ChangesNameAndColor()
    {
        // Arrange
        HttpClient admin = CreateClient(UserRole.ADMIN);
        HttpResponseMessage created = await admin.PostAsJsonAsync("/calendar/categories",
            new CalendarCategoryRequest { Name = "Training", Color = "#2563eb" });
        CalendarCategoryResult? category = await created.Content.ReadFromJsonAsync<CalendarCategoryResult>();

        // Act
        HttpResponseMessage response = await admin.PutAsJsonAsync($"/calendar/categories/{category!.Id}",
            new CalendarCategoryRequest { Name = "Sport", Color = "#00ff00" });
        CalendarCategoryResult? updated = await response.Content.ReadFromJsonAsync<CalendarCategoryResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        updated!.Name.ShouldBe("Sport");
        updated.Color.ShouldBe("#00ff00");

        await WithDbContext(async db =>
        {
            CalendarCategoryEntity? categoryEntity = await db.CalendarCategories.FirstOrDefaultAsync(x => x.Id == category.Id);
            categoryEntity.ShouldNotBeNull();
            categoryEntity.Name.ShouldBe("Sport");
        });
    }

    [Fact]
    public async Task DeleteAsync_RemovesCategoryFromEvents()
    {
        // Arrange
        HttpClient admin = CreateClient(UserRole.ADMIN);
        HttpResponseMessage created = await admin.PostAsJsonAsync("/calendar/categories",
            new CalendarCategoryRequest { Name = "Training", Color = "#2563eb" });
        CalendarCategoryResult? category = await created.Content.ReadFromJsonAsync<CalendarCategoryResult>();

        Guid eventId = Guid.NewGuid();
        await WithDbContext(async db =>
        {
            Guid ownerId = (await db.Users.FirstAsync()).Id;
            db.CalendarEvents.Add(new CalendarEventEntity
            {
                Id = eventId,
                Title = "Training",
                OwnerUserId = ownerId,
                CategoryId = category!.Id,
                StartAt = DateTimeOffset.UtcNow,
                EndAt = DateTimeOffset.UtcNow.AddHours(1),
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        });

        // Act
        HttpResponseMessage response = await admin.DeleteAsync($"/calendar/categories/{category!.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await WithDbContext(async db =>
        {
            CalendarEventEntity calendarEvent = await db.CalendarEvents.SingleAsync(e => e.Id == eventId);
            calendarEvent.CategoryId.ShouldBeNull();
        });
    }
}
