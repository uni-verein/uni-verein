using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

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
                using IServiceScope scope = _scopeFactory.CreateScope();
                CalendarReminderService reminderService =
                    scope.ServiceProvider.GetRequiredService<CalendarReminderService>();
                await reminderService.SendDueRemindersAsync();

                // Old notifications and reminder logs are cleaned up about once a day.
                if (ticks++ % 1440 == 0)
                    await reminderService.CleanupAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "CalendarReminderBackgroundService: Error while sending calendar reminders.");
            }
        }
    }
}
