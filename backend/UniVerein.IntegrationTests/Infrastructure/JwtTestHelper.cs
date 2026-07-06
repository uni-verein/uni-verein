using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using UniVerein.DAL.Entities.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace UniVerein.IntegrationTests.Infrastructure;

public static class JwtTestHelper
{
    private static readonly Guid AdminId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    public static string CreateAdminToken(IConfiguration configuration, string username = "admin", Guid? userId = null)
    {
        return CreateToken(configuration, userId ?? AdminId, username, UserRole.ADMIN);
    }

    public static string CreateUserToken(IConfiguration configuration, string username = "member", Guid? userId = null)
    {
        return CreateToken(configuration, userId ?? UserId, username, UserRole.USER);
    }

    public static string CreateFinancialUserToken(IConfiguration configuration, string username = "member",
        Guid? userId = null)
    {
        return CreateToken(configuration, userId ?? UserId, username, UserRole.FINANCIAL_MANAGER);
    }

    public static string CreateToken(
        IConfiguration configuration,
        Guid userId,
        string username,
        UserRole role,
        TimeSpan? lifetime = null)
    {
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
        SigningCredentials creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Issuer"],
            claims: claims,
            expires: DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromHours(1)),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static HttpClient WithBearerToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static HttpClient AsAdmin(this HttpClient client, IConfiguration configuration, Guid userId)
    {
        return client.WithBearerToken(CreateAdminToken(configuration, userId: userId));
    }

    public static HttpClient AsUser(this HttpClient client, IConfiguration configuration, Guid userId)
    {
        return client.WithBearerToken(CreateUserToken(configuration, userId: userId));
    }

    public static HttpClient AsFinancialUser(this HttpClient client, IConfiguration configuration, Guid userId)
    {
        return client.WithBearerToken(CreateFinancialUserToken(configuration, userId: userId));
    }
}