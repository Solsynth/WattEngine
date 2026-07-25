using Quartz;
using Task = System.Threading.Tasks.Task;

namespace WattEngine.Ideask.GitHub;

public class GitHubReconciliationJob(GitHubIntegrationService integrations) : IJob
{
    public async System.Threading.Tasks.Task Execute(IJobExecutionContext context) => await integrations.ReconcileAllAsync(context.CancellationToken);
}
