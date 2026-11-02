namespace Taxi.Infrastructure.Outbox;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>Runs <see cref="OutboxProcessor"/> every <c>Outbox:PollingIntervalSeconds</c>.</summary>
public sealed class OutboxBackgroundService(
    OutboxProcessor processor,
    IOptions<OutboxSettings> settings,
    ILogger<OutboxBackgroundService> logger) : BackgroundService
{
    private readonly OutboxProcessor processor = processor;
    private readonly OutboxSettings settings = settings.Value;
    private readonly ILogger<OutboxBackgroundService> logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(this.settings.PollingIntervalSeconds));

        do
        {
            try
            {
                await this.processor.ProcessAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // E.g. the database is briefly unreachable. Never let the loop die.
                this.logger.LogError(ex, "Outbox processing failed; retrying on the next tick.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
