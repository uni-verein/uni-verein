using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using UniVerein.Api.ApiRequests;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class SelfEnrollmentRateLimitTests : IntegrationTestBase
{
    public SelfEnrollmentRateLimitTests(UniVereinWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task PostSelfEnrollment_ExceedsConfiguredLimit_ReturnsTooManyRequests()
    {
        // Arrange
        HttpClient client = CreateClientWithRateLimit(permitLimit: 3);
        SelfEnrollmentRequest request = CreateSelfEnrollmentRequest();

        // Act
        List<HttpStatusCode> statuses = new();
        for (int i = 0; i < 4; i++)
        {
            HttpResponseMessage response = await client.PostAsJsonAsync("/self-enrollment", request);
            statuses.Add(response.StatusCode);
        }

        // Assert
        statuses[0].ShouldBe(HttpStatusCode.Forbidden);
        statuses[1].ShouldBe(HttpStatusCode.Forbidden);
        statuses[2].ShouldBe(HttpStatusCode.Forbidden);
        statuses[3].ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task GetFormData_ExceedsConfiguredReadLimit_ReturnsTooManyRequests()
    {
        // Arrange
        HttpClient client = CreateClientWithRateLimit(readPermitLimit: 2);

        // Act
        List<HttpStatusCode> statuses = new();
        for (int i = 0; i < 3; i++)
        {
            HttpResponseMessage response = await client.GetAsync("/self-enrollment/form-data");
            statuses.Add(response.StatusCode);
        }

        // Assert
        statuses[0].ShouldBe(HttpStatusCode.OK);
        statuses[1].ShouldBe(HttpStatusCode.OK);
        statuses[2].ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task ConfirmSelfEnrollment_ExceedsConfiguredConfirmLimit_ReturnsTooManyRequests()
    {
        // Arrange
        HttpClient client = CreateClientWithRateLimit(confirmPermitLimit: 2);

        // Act
        List<HttpStatusCode> statuses = new();
        for (int i = 0; i < 3; i++)
        {
            HttpResponseMessage response = await client.PostAsJsonAsync("/self-enrollment/confirm",
                new ConfirmSelfEnrollmentRequest { Token = "irrelevant-" + i });
            statuses.Add(response.StatusCode);
        }

        // Assert
        statuses[0].ShouldBe(HttpStatusCode.NotFound);
        statuses[1].ShouldBe(HttpStatusCode.NotFound);
        statuses[2].ShouldBe(HttpStatusCode.TooManyRequests);
    }

    // ---------------------------------------------------------------
    // Helper functions
    // ---------------------------------------------------------------
    private HttpClient CreateClientWithRateLimit(int? permitLimit = null, int? readPermitLimit = null,
        int? confirmPermitLimit = null)
    {
        WebApplicationFactory<UniVerein.Api.Startup> limitedFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                Dictionary<string, string?> overrides = new();
                if (permitLimit != null)
                    overrides["RateLimiting:SelfEnrollment:PermitLimit"] = permitLimit.Value.ToString();
                if (readPermitLimit != null)
                    overrides["RateLimiting:SelfEnrollment:ReadPermitLimit"] = readPermitLimit.Value.ToString();
                if (confirmPermitLimit != null)
                    overrides["RateLimiting:SelfEnrollment:ConfirmPermitLimit"] = confirmPermitLimit.Value.ToString();

                config.AddInMemoryCollection(overrides);
            });
        });

        return limitedFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });
    }

    private static SelfEnrollmentRequest CreateSelfEnrollmentRequest()
    {
        return new SelfEnrollmentRequest
        {
            Gender = Gender.MALE,
            FirstName = "Rate",
            LastName = "Limited",
            Birthday = DateTimeOffset.UtcNow.AddYears(-20),
            Street = "street",
            PostalCode = "24103",
            City = "Kiel",
            CountryCode = "DE",
            Email = $"{Guid.NewGuid()}@test.de",
            BulkMail = BulkMail.ALLOWED,
            StartOfStudies = DateTimeOffset.UtcNow,
            Motivation = "Studiere Informatik.",
            IBAN = Guid.NewGuid().ToString("N"),
            Bic = "DEUTDEDEXXX",
            EntryDate = DateTimeOffset.UtcNow
        };
    }
}
