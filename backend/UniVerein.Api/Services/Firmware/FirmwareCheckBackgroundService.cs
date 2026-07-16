using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace UniVerein.Api.Services.Firmware
{
    public class FirmwareCheckBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _interval = TimeSpan.FromHours(1);

        public FirmwareCheckBackgroundService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            using PeriodicTimer timer = new(_interval);

            while (!cancellationToken.IsCancellationRequested && await timer.WaitForNextTickAsync(cancellationToken))
            {
                await CheckFirmwareAsync(cancellationToken);
            }
        }

        private async Task CheckFirmwareAsync(CancellationToken cancellationToken)
        {
            try
            {
                FirmwareService firmwareService = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<FirmwareService>();
                await firmwareService.CheckLatestFirmwareAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "FirmwareCheckBackgroundService: Error while firmware check.");
            }
        }
    }
}