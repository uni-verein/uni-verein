using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using UniVerein.Api.ApiRequests;
using UniVerein.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class AuthLoginRateLimitTests : IntegrationTestBase
{
    public AuthLoginRateLimitTests(UniVereinWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Login_ExceedsConfiguredLimit_ReturnsTooManyRequests()
    {
        // Arrange
        HttpClient client = CreateClientWithRateLimit(permitLimit: 3);
        LoginRequest request = new()
        {
            Username = "does-not-exist",
            Password = "irrelevant"
        };

        // Act
        List<HttpStatusCode> statuses = new();
        for (int i = 0; i < 4; i++)
        {
            HttpResponseMessage response = await client.PostAsJsonAsync("/auth/login", request);
            statuses.Add(response.StatusCode);
        }

        // Assert
        statuses[0].ShouldBe(HttpStatusCode.Unauthorized);
        statuses[1].ShouldBe(HttpStatusCode.Unauthorized);
        statuses[2].ShouldBe(HttpStatusCode.Unauthorized);
        statuses[3].ShouldBe(HttpStatusCode.TooManyRequests);
    }

    // ---------------------------------------------------------------
    // Helper functions
    // ---------------------------------------------------------------
    private HttpClient CreateClientWithRateLimit(int permitLimit)
    {
        WebApplicationFactory<UniVerein.Api.Startup> limitedFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                Dictionary<string, string?> overrides = new()
                {
                    ["RateLimiting:AuthLogin:PermitLimit"] = permitLimit.ToString()
                };

                config.AddInMemoryCollection(overrides);
            });
        });

        return limitedFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });
    }
}
