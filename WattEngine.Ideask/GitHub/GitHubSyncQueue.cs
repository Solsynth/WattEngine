using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;

namespace WattEngine.Ideask.GitHub;

public class GitHubSyncQueue
{
    private readonly Channel<Guid> queue = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });
    public ValueTask EnqueueAsync(Guid integrationId, CancellationToken cancellationToken = default) => queue.Writer.WriteAsync(integrationId, cancellationToken);
    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken) => queue.Reader.ReadAllAsync(cancellationToken);
}

public class GitHubSyncWorker(IServiceScopeFactory scopes, GitHubSyncQueue queue, ILogger<GitHubSyncWorker> logger) : BackgroundService
{
    protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var integrationId in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var integrations = scope.ServiceProvider.GetRequiredService<GitHubIntegrationService>();
                await integrations.ReconcileIntegrationAsync(integrationId, stoppingToken);
            }
            catch (Exception ex) { logger.LogWarning(ex, "Queued GitHub synchronization failed for {IntegrationId}", integrationId); }
        }
    }
}
