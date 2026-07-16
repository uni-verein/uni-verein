using System.Net;
using System.Net.Http.Json;
using System.Text;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace UniVerein.IntegrationTests.Infrastructure;

public abstract class IntegrationTestBase : IClassFixture<UniVereinWebApplicationFactory>, IAsyncLifetime
{
    protected readonly UniVereinWebApplicationFactory Factory;
    protected readonly Guid UserId = Guid.NewGuid();
    private IServiceScope _scope = null!;
    protected IServiceProvider ScopedServices => _scope.ServiceProvider;

    protected IntegrationTestBase(UniVereinWebApplicationFactory factory)
    {
        Factory = factory;
        _scope = Factory.Services.CreateScope();
    }

    public virtual async Task InitializeAsync()
    {
        await Factory.InitializeAsync();
        await Factory.ResetDatabaseAsync();
    }

    public virtual Task DisposeAsync()
    {
        _scope.Dispose();
        return Task.CompletedTask;
    }

    protected HttpClient CreateClient()
    {
        return Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });
    }

    protected HttpClient CreateAdminClient()
    {
        IConfiguration configuration = Factory.Services.GetRequiredService<IConfiguration>();
        return CreateClient().AsAdmin(configuration, UserId);
    }

    private HttpClient CreateUserClient()
    {
        IConfiguration configuration = Factory.Services.GetRequiredService<IConfiguration>();
        return CreateClient().AsUser(configuration, UserId);
    }

    protected HttpClient CreateFinancialUserClient()
    {
        IConfiguration configuration = Factory.Services.GetRequiredService<IConfiguration>();
        return CreateClient().AsFinancialUser(configuration, UserId);
    }

    protected async Task WithDbContext(Func<AppDbContext, Task> action)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await action(db);
    }

    protected T GetService<T>() where T : notnull
    {
        return ScopedServices.GetRequiredService<T>();
    }

    protected HttpClient CreateClient(UserRole role)
    {
        if (role == UserRole.ADMIN)
            return CreateAdminClient();
        if (role == UserRole.USER)
            return CreateUserClient();
        if (role == UserRole.FINANCIAL_MANAGER)
            return CreateFinancialUserClient();

        return CreateClient();
    }

    protected static (HttpClient Client, FakeHttpMessageHandler Handler) BuildHttpClient<T>(T? body)
    {
        FakeHttpMessageHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = body == null
                ? new StringContent("null", Encoding.UTF8, "application/json")
                : JsonContent.Create(body)
        });
        return (new HttpClient(handler), handler);
    }
}