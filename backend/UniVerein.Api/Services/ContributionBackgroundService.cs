using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using UniVerein.Api.Tenancy;

namespace UniVerein.Api.Services;

public class ContributionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _interval = TimeSpan.FromHours(1);

    public ContributionBackgroundService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await ProcessContributionsAsync();
            await Task.Delay(_interval, cancellationToken);
        }
    }

    private async Task ProcessContributionsAsync()
    {
        try
        {
            await _scopeFactory.ForEachTenantAsync(async services =>
            {
                try
                {
                    await services.GetRequiredService<ContributionService>().GenerateDueContributions();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "ContributionBackgroundService: Error on contribution run of tenant {TenantId}",
                        services.GetRequiredService<TenantContext>().TenantId);
                }
            });
            Log.Debug("ContributionBackgroundService: Contribution run finished.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ContributionBackgroundService: Error on contribution background service");
        }
    }
}