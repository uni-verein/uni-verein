using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Services;
using UniVerein.DAL.Entities;
using UniVerein.IntegrationTests.Infrastructure;
using Shouldly;
using UniVerein.DAL.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class UsersControllerTests : IntegrationTestBase
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly CryptoService _cryptoService;

    public UsersControllerTests(UniVereinWebApplicationFactory factory) : base(factory)
    {
        _jsonSerializerOptions = new()
        {
            Converters = { new JsonStringEnumConverter() }
        };
        _cryptoService = GetService<CryptoService>();
    }

    public override async Task InitializeAsync()
    {
        await WithDbContext(async db =>
        {
            db.Users.RemoveRange(db.Users.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    public override async Task DisposeAsync()
    {
        await WithDbContext(async db =>
        {
            db.Users.RemoveRange(db.Users.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    [Fact]
    public async Task GetUsers_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/users");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetUsers_WithExpiredToken_Unauthorized(UserRole role)
    {
        // Arrange
        IConfiguration configuration = Factory.Services.GetRequiredService<IConfiguration>();
        var expiredToken = JwtTestHelper.CreateToken(
            configuration,
            userId: Guid.NewGuid(),
            username: "expired",
            role: role,
            lifetime: TimeSpan.FromMinutes(-5));

        var client = CreateClient().WithBearerToken(expiredToken);

        // Act
        var response = await client.GetAsync("/users");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetUsers_Forbidden(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.GetAsync("/users");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetUsers_Authorized()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.GetAsync("/users");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetUsers_Success()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        List<UserEntity> users = new();
        foreach (var index in Enumerable.Range(0, 5))
            users.Add(await CreateUserEntity(username: index.ToString()));

        // Act
        HttpResponseMessage response = await client.GetAsync("/users");
        UserResults? results = await response.Content.ReadFromJsonAsync<UserResults>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
        results.Total.ShouldBe(5);
        foreach (UserEntity user in users)
        {
            UserResult? result = results.Items.FirstOrDefault(x => x.Id == user.Id);
            result.ShouldNotBeNull();
            CompareUser(user, result);
        }

        results.Items.Select(x => int.Parse(x.Username)).ToArray().ShouldBeEquivalentTo(Enumerable.Range(0, 5).ToArray());
    }

    [Fact]
    public async Task GetAccountUsers_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/users/account");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetAccountUsers_WithExpiredToken_Unauthorized(UserRole role)
    {
        // Arrange
        IConfiguration configuration = Factory.Services.GetRequiredService<IConfiguration>();
        var expiredToken = JwtTestHelper.CreateToken(
            configuration,
            userId: Guid.NewGuid(),
            username: "expired",
            role: role,
            lifetime: TimeSpan.FromMinutes(-5));

        var client = CreateClient().WithBearerToken(expiredToken);

        // Act
        var response = await client.GetAsync("/users/account");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    [InlineData(UserRole.ADMIN)]
    public async Task GetAccountUsers_UserNotFound(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.GetAsync("/users/account");
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        result.ErrorMessage.ShouldBe("User not found.");
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    [InlineData(UserRole.ADMIN)]
    public async Task GetAccountUsers_Authorized(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        await CreateUserEntity(id: UserId);

        // Act
        HttpResponseMessage response = await client.GetAsync("/users/account");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    [InlineData(UserRole.ADMIN)]
    public async Task GetAccountUsers_Success(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        UserEntity user = await CreateUserEntity(id: UserId);

        // Act
        HttpResponseMessage response = await client.GetAsync("/users/account");
        UserResult? result = await response.Content.ReadFromJsonAsync<UserResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        CompareUser(user, result);
    }

    // ---------------------------------------------------------------
    // CREATE /api/users
    // ---------------------------------------------------------------

    [Fact]
    public async Task CreateUser_WithoutToken_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/users", CreateUserRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task CreateUser_Forbidden(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/users", CreateUserRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("testtesttesttesttesttesttesttesttesttesttesttesttest")]
    public async Task CreateUser_UsernameIncorrect_BadRequest(string username)
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        UserRequest user = CreateUserRequest(username);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/users", user);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        result.MoreInfo.ShouldBe("Username length must be greater than 0 or less 51 characters.");
    }

    [Theory]
    [InlineData("testtest")]
    [InlineData("testtesttesttesttesttesttesttesttesttesttesttesttest")]
    public async Task CreateUser_PasswordIncorrect_BadRequest(string password)
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        UserRequest user = CreateUserRequest(password: password);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/users", user);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        result.MoreInfo.ShouldBe("Password length must be greater than 9 or less 51 characters.");
    }

    [Fact]
    public async Task CreateUser_MailIncorrect_BadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        UserRequest user = CreateUserRequest(mail: "testtesttesttesttesttesttesttesttesttesttesttesttest");

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/users", user);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        result.MoreInfo.ShouldBe("Email length must be less 50 characters.");
    }

    [Fact]
    public async Task CreateUser_WithDuplicateUsername_Conflict()
    {
        var client = CreateAdminClient();
        UserEntity userEntity = await CreateUserEntity();
        UserRequest userRequest = CreateUserRequest(userEntity.Username);

        // Act
        var response = await client.PostAsJsonAsync("/users", userRequest);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.Conflict);
        result.MoreInfo.ShouldBe("User with same name already exists. Try a other name.");
    }

    [Fact]
    public async Task CreateUser_Success()
    {
        // Arrange
        var client = CreateClient(UserRole.ADMIN);
        UserRequest request = CreateUserRequest();

        // Act
        var response = await client.PostAsJsonAsync("/users", request);
        UserResult? result = await response.Content.ReadFromJsonAsync<UserResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        result.ShouldNotBeNull();
        result.Username.ShouldBe(request.Username);
        await WithDbContext(async db =>
        {
            UserEntity? user = await db.Users.FirstOrDefaultAsync(u => u.Username == result.Username);
            user.ShouldNotBeNull();
            CompareUser(user, result);
        });
    }

    // ---------------------------------------------------------------
    // UPDATE /api/members/{id}
    // ---------------------------------------------------------------

    [Fact]
    public async Task UpdateUser_WithoutToken_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response =
            await client.PatchAsJsonAsync($"/users/{Guid.NewGuid()}", CreateUpdateUserRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task UpdateUser_Forbidden(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response =
            await client.PatchAsJsonAsync($"/users/{Guid.NewGuid()}", CreateUpdateUserRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateUser_NotFound()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response =
            await client.PatchAsJsonAsync($"/users/{Guid.NewGuid()}", CreateUpdateUserRequest());
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        result.ErrorMessage.ShouldBe("User not found.");
    }

    [Fact]
    public async Task UpdateUser_UsernameToLong_BadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        UserEntity userEntity = await CreateUserEntity();
        UserUpdateRequest request = CreateUpdateUserRequest("testtesttesttesttesttesttesttesttesttesttesttesttest");

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/users/{userEntity.Id}", request);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        result.MoreInfo.ShouldBe("Username length must be greater than 0 or less 51 characters.");
    }

    [Fact]
    public async Task UpdateUser_MailToLong_BadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        UserEntity userEntity = await CreateUserEntity();
        UserUpdateRequest request =
            CreateUpdateUserRequest(mail: "testtesttesttesttesttesttesttesttesttesttesttesttest");

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/users/{userEntity.Id}", request);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        result.MoreInfo.ShouldBe("Email length must be less 51 characters.");
    }

    [Fact]
    public async Task UpdateUser_WithDuplicateUsername_Conflict()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        UserEntity firstUserEntity = await CreateUserEntity();
        UserEntity testUser = await CreateUserEntity();
        UserUpdateRequest request = CreateUpdateUserRequest(firstUserEntity.Username);

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/users/{testUser.Id}", request);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.Conflict);
        result.MoreInfo.ShouldBe("User with same name already exists. Try a other name.");
    }

    [Theory]
    [InlineData("testtest")]
    [InlineData("testtesttesttesttesttesttesttesttesttesttesttesttest")]
    public async Task UpdateUser_PasswordIncorrect_BadRequest(string password)
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        UserEntity userEntity = await CreateUserEntity();
        UserUpdateRequest request = CreateUpdateUserRequest(password: password);

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/users/{userEntity.Id}", request);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        result.MoreInfo.ShouldBe("Password length must be greater than 9 or less 51 characters.");
    }

    [Fact]
    public async Task UpdateUser_Success()
    {
        // Arrange
        var client = CreateClient(UserRole.ADMIN);
        UserEntity userEntity = await CreateUserEntity();
        UserUpdateRequest request = CreateUpdateUserRequest();

        // Act
        var response = await client.PatchAsJsonAsync($"/users/{userEntity.Id}", request);
        UserResult? result = await response.Content.ReadFromJsonAsync<UserResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Username.ShouldBe(request.Username);
        await WithDbContext(async db =>
        {
            UserEntity? user = await db.Users.FirstOrDefaultAsync(u => u.Username == result.Username);
            user.ShouldNotBeNull();
            CompareUser(user, result);
        });
    }

    // ---------------------------------------------------------------
    // UPDATE /api/users/account
    // ---------------------------------------------------------------

    [Fact]
    public async Task UpdateAccountUser_WithoutToken_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/users/account", CreateUpdateUserRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    [InlineData(UserRole.ADMIN)]
    public async Task UpdateAccountUser_NotFound(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync("/users/account", CreateUpdateUserRequest());
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        result.ErrorMessage.ShouldBe("User not found.");
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    [InlineData(UserRole.ADMIN)]
    public async Task UpdateAccountUser_UsernameToLong_BadRequest(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        await CreateUserEntity(id: UserId);
        UserUpdateRequest request = CreateUpdateUserRequest("testtesttesttesttesttesttesttesttesttesttesttesttest");

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync("/users/account", request);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        result.MoreInfo.ShouldBe("Username length must be greater than 0 or less 51 characters.");
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    [InlineData(UserRole.ADMIN)]
    public async Task UpdateAccountUser_WithDuplicateUsername_Conflict(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        UserEntity firstUserEntity = await CreateUserEntity();
        await CreateUserEntity(id: UserId);
        UserUpdateRequest request = CreateUpdateUserRequest(firstUserEntity.Username);

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync("/users/account", request);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.Conflict);
        result.MoreInfo.ShouldBe("User with same name already exists. Try a other name.");
    }

    [Theory]
    [InlineData(UserRole.USER, "testtest")]
    [InlineData(UserRole.USER, "testtesttesttesttesttesttesttesttesttesttesttesttest")]
    [InlineData(UserRole.FINANCIAL_MANAGER, "testtest")]
    [InlineData(UserRole.FINANCIAL_MANAGER, "testtesttesttesttesttesttesttesttesttesttesttesttest")]
    [InlineData(UserRole.ADMIN, "testtest")]
    [InlineData(UserRole.ADMIN, "testtesttesttesttesttesttesttesttesttesttesttesttest")]
    public async Task UpdateAccountUser_PasswordIncorrect_BadRequest(UserRole role, string password)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        await CreateUserEntity(id: UserId);
        UserUpdateRequest request = CreateUpdateUserRequest(password: password);

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/users/account", request);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        result.MoreInfo.ShouldBe("Password length must be greater than 9 or less 51 characters.");
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    [InlineData(UserRole.ADMIN)]
    public async Task UpdateAccountUser_Success(UserRole role)
    {
        // Arrange
        var client = CreateClient(role);
        await CreateUserEntity(id: UserId);
        UserUpdateRequest request = CreateUpdateUserRequest();

        // Act
        var response = await client.PatchAsJsonAsync($"/users/account", request);
        UserResult? result = await response.Content.ReadFromJsonAsync<UserResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Username.ShouldBe(request.Username);
        await WithDbContext(async db =>
        {
            UserEntity? user = await db.Users.FirstOrDefaultAsync(u => u.Username == result.Username);
            user.ShouldNotBeNull();
            CompareUser(user, result);
        });
    }

    [Fact]
    public async Task UpdateAccountUser_RoleShouldBeNotChanged_Success()
    {
        // Arrange
        var client = CreateClient(UserRole.ADMIN);
        await CreateUserEntity(id: UserId, role: UserRole.ADMIN);
        UserUpdateRequest request = CreateUpdateUserRequest();

        // Act
        var response = await client.PatchAsJsonAsync($"/users/account", request);
        UserResult? result = await response.Content.ReadFromJsonAsync<UserResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Username.ShouldBe(request.Username);
        await WithDbContext(async db =>
        {
            UserEntity? user = await db.Users.FirstOrDefaultAsync(u => u.Username == result.Username);
            user.ShouldNotBeNull();
            user.Role.ShouldNotBe((UserRole)request.Role!);
            user.Role.ShouldBe(UserRole.ADMIN);
            CompareUser(user, result);
        });
    }

    // ---------------------------------------------------------------
    // DELETE /api/members/{id}
    // ---------------------------------------------------------------

    [Fact]
    public async Task DeleteUser_WithoutToken_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/users/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task DeleteUser_Forbidden(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/users/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteUser_NotFound()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/users/{Guid.NewGuid()}");
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        result.ErrorMessage.ShouldBe("User not found.");
    }

    [Fact]
    public async Task DeleteUser_AsAdmin_ReturnsNoOk()
    {
        HttpClient client = CreateAdminClient();
        UserEntity userEntity = await CreateUserEntity();

        // Act
        var response = await client.DeleteAsync($"/users/{userEntity.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ---------------------------------------------------------------
    // Helper functions
    // ---------------------------------------------------------------

    private async Task<UserEntity> CreateUserEntity(Guid? id = null, string? username = null, UserRole? role = null)
    {
        UserEntity userEntity = new()
        {
            Id = id ?? Guid.NewGuid(),
            Username = username ?? Guid.NewGuid().ToString(),
            PasswordHash = _cryptoService.Hash(Guid.NewGuid().ToString()),
            Email = _cryptoService.Encrypt("test@gmail.com"),
            Role = role ?? UserRole.USER,
        };

        await WithDbContext(async db =>
        {
            await db.Users.AddAsync(userEntity);
            await db.SaveChangesAsync();
        });

        return userEntity;
    }

    private void CompareUser(UserEntity entity, UserResult result)
    {
        entity.Id.ShouldBe(result.Id);
        entity.Username.ShouldBe(result.Username);
        entity.Role.ShouldBe(result.Role);
        _cryptoService.Decrypt(entity.Email).ShouldBe(result.Email);
    }

    private UserRequest CreateUserRequest(string? username = null, string? password = null, string? mail = null)
    {
        return new UserRequest()
        {
            Username = username ?? Guid.NewGuid().ToString(),
            Password = password ?? Guid.NewGuid().ToString(),
            Email = mail ?? "test@gmail.com",
            Role = UserRole.USER
        };
    }

    private UserUpdateRequest CreateUpdateUserRequest(string? username = null, string? password = null,
        string? mail = null)
    {
        return new UserUpdateRequest()
        {
            Username = username ?? Guid.NewGuid().ToString(),
            Password = password ?? Guid.NewGuid().ToString(),
            Email = mail ?? "test2@gmail.com",
            Role = UserRole.USER
        };
    }
}