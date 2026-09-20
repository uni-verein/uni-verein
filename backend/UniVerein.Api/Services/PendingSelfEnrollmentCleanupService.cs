using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace UniVerein.Api.Services;

public class PendingSelfEnrollmentCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _interval = TimeSpan.FromHours(1);

    public PendingSelfEnrollmentCleanupService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await CleanupExpiredSubmissionsAsync();
            await Task.Delay(_interval, cancellationToken);
        }
    }

    private async Task CleanupExpiredSubmissionsAsync()
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            PendingSelfEnrollmentService service = scope.ServiceProvider.GetRequiredService<PendingSelfEnrollmentService>();

            int removed = await service.CleanupExpiredAsync();
            if (removed > 0)
                Log.Information(
                    $"PendingSelfEnrollmentCleanupService: Removed {removed} expired, unconfirmed self-enrollment submission(s).");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PendingSelfEnrollmentCleanupService: Error while cleaning up expired self-enrollment submissions");
        }
    }
}
