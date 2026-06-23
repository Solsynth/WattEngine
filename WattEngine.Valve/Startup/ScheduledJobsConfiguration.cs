using Quartz;

namespace WattEngine.Valve.Startup;

public static class ScheduledJobsConfiguration
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAppScheduledJobs()
        {
            services.AddQuartz(q =>
            {
                var recyclingJobKey = new JobKey("AppDatabaseRecycling");
                q.AddJob<AppDatabaseRecyclingJob>(opts => opts.WithIdentity(recyclingJobKey));
                q.AddTrigger(opts => opts
                    .ForJob(recyclingJobKey)
                    .WithIdentity("AppDatabaseRecycling-trigger")
                    .WithCronSchedule("0 0 3 * * ?") // 3 AM daily
                );
            });

            services.AddQuartzHostedService(opts =>
            {
                opts.WaitForJobsToComplete = true;
            });

            return services;
        }
    }
}
