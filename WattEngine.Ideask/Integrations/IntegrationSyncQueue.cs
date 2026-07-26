using System.Threading.Channels;

namespace WattEngine.Ideask.Integrations;

public class IntegrationSyncQueue
{
    private readonly Channel<IntegrationJob> queue = Channel.CreateUnbounded<IntegrationJob>(new UnboundedChannelOptions { SingleReader = true });
    public ValueTask EnqueueAsync(IntegrationJob job, CancellationToken cancellationToken = default) => queue.Writer.WriteAsync(job, cancellationToken);
    public IAsyncEnumerable<IntegrationJob> ReadAllAsync(CancellationToken cancellationToken) => queue.Reader.ReadAllAsync(cancellationToken);
}

public class IntegrationSyncWorker(IntegrationSyncQueue queue, IServiceScopeFactory scopes, ILogger<IntegrationSyncWorker> logger) : BackgroundService
{
    protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var providers = scope.ServiceProvider.GetRequiredService<IntegrationProviderRegistry>();
                await providers.Get(job.Provider).ReconcileAsync(job.IntegrationId, stoppingToken);
            }
            catch (Exception ex) { logger.LogWarning(ex, "Queued {Provider} synchronization failed for {IntegrationId}", job.Provider, job.IntegrationId); }
        }
    }
}
