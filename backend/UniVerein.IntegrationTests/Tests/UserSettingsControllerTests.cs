using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class UserSettingsControllerTests : IntegrationTestBase
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public UserSettingsControllerTests(UniVereinWebApplicationFactory factory) : base(factory)
    {
        _jsonSerializerOptions = new()
        {
            Converters = { new JsonStringEnumConverter() }
        };
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await WithDbContext(async db =>
        {
            db.UserSettings.RemoveRange(db.UserSettings.AsQueryable());
            db.Users.RemoveRange(db.Users.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    // ---------------------------------------------------------------
    // GET /users/account/settings
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetAllAsync_WithoutToken_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/users/account/settings");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAllAsync_NoSettingsYet_ReturnsEmptyList()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.FINANCIAL_MANAGER);
        await CreateUserEntity(id: UserId, role: UserRole.FINANCIAL_MANAGER);

        // Act
        HttpResponseMessage response = await client.GetAsync("/users/account/settings");
        List<UserSettingResult>? result =
            await response.Content.ReadFromJsonAsync<List<UserSettingResult>>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_OnlyReturnsSettingsOfCurrentUser()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.FINANCIAL_MANAGER);
        await CreateUserEntity(id: UserId, role: UserRole.FINANCIAL_MANAGER);
        UserEntity otherUser = await CreateUserEntity(role: UserRole.FINANCIAL_MANAGER);

        await WithDbContext(async db =>
        {
            UserEntity ownUser = await db.Users.SingleAsync(u => u.Id == UserId);
            UserEntity fetchedOtherUser = await db.Users.SingleAsync(u => u.Id == otherUser.Id);
            await db.UserSettings.AddRangeAsync(
                new UserSettingEntity
                {
                    UserId = UserId,
                    User = ownUser,
                    Type = UserSettingType.RECEIPT_NOTIFICATION,
                    Enabled = true
                },
                new UserSettingEntity
                {
                    UserId = otherUser.Id,
                    User = fetchedOtherUser,
                    Type = UserSettingType.RECEIPT_NOTIFICATION,
                    Enabled = false
                });
            await db.SaveChangesAsync();
        });

        // Act
        HttpResponseMessage response = await client.GetAsync("/users/account/settings");
        List<UserSettingResult>? result =
            await response.Content.ReadFromJsonAsync<List<UserSettingResult>>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].Type.ShouldBe(UserSettingType.RECEIPT_NOTIFICATION);
        result[0].Enabled.ShouldBeTrue();
    }

    // ---------------------------------------------------------------
    // PUT /users/account/settings/{type}
    // ---------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_WithoutToken_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync(
            "/users/account/settings/RECEIPT_NOTIFICATION", new UpdateUserSettingRequest { Enabled = true });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateAsync_NoExistingRow_CreatesNewSetting()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.FINANCIAL_MANAGER);
        await CreateUserEntity(id: UserId, role: UserRole.FINANCIAL_MANAGER);

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync(
            "/users/account/settings/RECEIPT_NOTIFICATION", new UpdateUserSettingRequest { Enabled = true });
        UserSettingResult? result = await response.Content.ReadFromJsonAsync<UserSettingResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Type.ShouldBe(UserSettingType.RECEIPT_NOTIFICATION);
        result.Enabled.ShouldBeTrue();

        await WithDbContext(async db =>
        {
            List<UserSettingEntity> settings = await db.UserSettings.Where(s => s.UserId == UserId).ToListAsync();
            settings.Count.ShouldBe(1);
            settings[0].Enabled.ShouldBeTrue();
        });
    }

    [Fact]
    public async Task UpdateAsync_ExistingRow_UpdatesInPlaceWithoutDuplicate()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.FINANCIAL_MANAGER);
        await CreateUserEntity(id: UserId, role: UserRole.FINANCIAL_MANAGER);
        await client.PutAsJsonAsync(
            "/users/account/settings/RECEIPT_NOTIFICATION", new UpdateUserSettingRequest { Enabled = true });

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync(
            "/users/account/settings/RECEIPT_NOTIFICATION", new UpdateUserSettingRequest { Enabled = false });
        UserSettingResult? result = await response.Content.ReadFromJsonAsync<UserSettingResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Enabled.ShouldBeFalse();

        await WithDbContext(async db =>
        {
            List<UserSettingEntity> settings = await db.UserSettings.Where(s => s.UserId == UserId).ToListAsync();
            settings.Count.ShouldBe(1);
            settings[0].Enabled.ShouldBeFalse();
        });
    }

    // ---------------------------------------------------------------
    // Helper functions
    // ---------------------------------------------------------------

    private async Task<UserEntity> CreateUserEntity(Guid? id = null, UserRole? role = null)
    {
        UserEntity userEntity = new()
        {
            Id = id ?? Guid.NewGuid(),
            Username = Guid.NewGuid().ToString(),
            PasswordHash = Guid.NewGuid().ToString(),
            Role = role ?? UserRole.USER
        };

        await WithDbContext(async db =>
        {
            await db.Users.AddAsync(userEntity);
            await db.SaveChangesAsync();
        });

        return userEntity;
    }
}
