using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using UniVerein.Api.Tenancy;

namespace UniVerein.Api.Services.Calendar;

public class CalendarReminderBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);

    public CalendarReminderBackgroundService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using PeriodicTimer timer = new(_interval);
        int ticks = 0;

        while (!cancellationToken.IsCancellationRequested && await timer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                bool cleanup = ticks++ % 1440 == 0;

                await _scopeFactory.ForEachTenantAsync(async services =>
                {
                    try
                    {
                        CalendarReminderService reminderService = services.GetRequiredService<CalendarReminderService>();
                        await reminderService.SendDueRemindersAsync();

                        // Old notifications and reminder logs are cleaned up about once a day.
                        if (cleanup)
                            await reminderService.CleanupAsync();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "CalendarReminderBackgroundService: Error while sending calendar reminders of tenant {TenantId}.",
                            services.GetRequiredService<TenantContext>().TenantId);
                    }
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "CalendarReminderBackgroundService: Error while sending calendar reminders.");
            }
        }
    }
}
