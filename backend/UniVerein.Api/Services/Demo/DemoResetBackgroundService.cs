using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace UniVerein.Api.Services.Demo;

public class DemoResetBackgroundService : BackgroundService
{
    public static readonly TimeSpan ResetTime = new(3, 0, 0);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _time;

    public DemoResetBackgroundService(IServiceScopeFactory scopeFactory, TimeProvider time)
    {
        _scopeFactory = scopeFactory;
        _time = time;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(DelayUntilNextReset(_time.GetUtcNow()), _time, cancellationToken);
            await ResetAsync();
        }
    }

    public static TimeSpan DelayUntilNextReset(DateTimeOffset now)
    {
        DateTimeOffset next = new(now.UtcDateTime.Date + ResetTime, TimeSpan.Zero);
        if (next <= now)
            next = next.AddDays(1);

        return next - now;
    }

    private async Task ResetAsync()
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<DemoDataService>().ResetAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "DemoResetBackgroundService: Error while resetting the demo data");
        }
    }
}
