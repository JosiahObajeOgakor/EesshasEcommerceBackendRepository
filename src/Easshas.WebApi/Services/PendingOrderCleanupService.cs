using System;
using System.Threading;
using System.Threading.Tasks;
using Easshas.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Easshas.WebApi.Services
{
    public class PendingOrderCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<PendingOrderCleanupService> _logger;

        public PendingOrderCleanupService(IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<PendingOrderCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _config = config;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var intervalSeconds = _config.GetValue<int?>("App:PendingOrderCleanup:IntervalSeconds") ?? 300; // default 5 minutes
            var olderThanMinutes = _config.GetValue<int?>("App:PendingOrderCleanup:OlderThanMinutes") ?? 30; // default 30 minutes

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
                    var released = await orders.ReleaseExpiredPendingOrdersAsync(olderThanMinutes);
                    if (released > 0) _logger.LogInformation("Released {Count} expired pending orders", released);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error running pending order cleanup");
                }

                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
        }
    }
}
