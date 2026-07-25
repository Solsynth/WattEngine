using Quartz;
using WattEngine.Ideask.GitHub;

namespace WattEngine.Ideask.Startup;

public static class ScheduledJobsConfiguration
{
    public static IServiceCollection AddAppScheduledJobs(this IServiceCollection services)
    {
        services.AddQuartz(q =>
        {
            var appDatabaseRecyclingJob = new JobKey("AppDatabaseRecycling");
            q.AddJob<AppDatabaseRecyclingJob>(opts => opts.WithIdentity(appDatabaseRecyclingJob));
            q.AddTrigger(opts => opts
                .ForJob(appDatabaseRecyclingJob)
                .WithIdentity("AppDatabaseRecyclingTrigger")
                .WithCronSchedule("0 0 0 * * ?"));
            var githubSyncJob = new JobKey("GitHubReconciliation");
            q.AddJob<GitHubReconciliationJob>(opts => opts.WithIdentity(githubSyncJob));
            q.AddTrigger(opts => opts.ForJob(githubSyncJob).WithIdentity("GitHubReconciliationTrigger")
                .WithCronSchedule("0 0 */15 * * ?"));
        });
        services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

        return services;
    }
}
