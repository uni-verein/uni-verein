using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

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
            using IServiceScope scope = _scopeFactory.CreateScope();
            ContributionService contributionService = scope.ServiceProvider.GetRequiredService<ContributionService>();

            await contributionService.DeletePaidContributions();
            await contributionService.GenerateDueContributions();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ContributionBackgroundService: Error on contribution background service");
        }
    }
}