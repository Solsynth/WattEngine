namespace WattEngine.Ideask.Integrations;

public enum IntegrationProvider
{
    GitHub = 1
}

public readonly record struct IntegrationJob(IntegrationProvider Provider, Guid IntegrationId);

/// <summary>Provider boundary for task systems such as GitHub, GitLab, or Jira.</summary>
public interface ITaskIntegrationProvider
{
    IntegrationProvider Provider { get; }
    System.Threading.Tasks.Task ReconcileAsync(Guid integrationId, CancellationToken cancellationToken = default);
    System.Threading.Tasks.Task SyncTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
    System.Threading.Tasks.Task SyncCommentAsync(Guid commentId, bool deleted = false, CancellationToken cancellationToken = default);
}

public class IntegrationProviderRegistry(IEnumerable<ITaskIntegrationProvider> providers)
{
    private readonly IReadOnlyDictionary<IntegrationProvider, ITaskIntegrationProvider> providers = providers.ToDictionary(provider => provider.Provider);
    public ITaskIntegrationProvider Get(IntegrationProvider provider) => providers.TryGetValue(provider, out var value)
        ? value : throw new KeyNotFoundException($"Integration provider {provider} is not registered.");
    public IReadOnlyCollection<ITaskIntegrationProvider> All => providers.Values.ToArray();
}

/// <summary>Provider-neutral dispatch used by task and comment services.</summary>
public class IntegrationOrchestrator(IntegrationProviderRegistry registry)
{
    public System.Threading.Tasks.Task SyncTaskAsync(Guid taskId, CancellationToken cancellationToken = default) =>
        System.Threading.Tasks.Task.WhenAll(registry.All.Select(provider => provider.SyncTaskAsync(taskId, cancellationToken)));
    public System.Threading.Tasks.Task SyncCommentAsync(Guid commentId, bool deleted = false, CancellationToken cancellationToken = default) =>
        System.Threading.Tasks.Task.WhenAll(registry.All.Select(provider => provider.SyncCommentAsync(commentId, deleted, cancellationToken)));
}
