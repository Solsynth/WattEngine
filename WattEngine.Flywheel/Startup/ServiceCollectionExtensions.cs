using System.Text.Json;
using DysonNetwork.Shared.Cache;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using WattEngine.Flywheel.Flywheel;

namespace WattEngine.Flywheel.Startup;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAppServices(IConfiguration configuration)
        {
            services.AddDbContext<AppDatabase>();
            services.AddHttpContextAccessor();
            services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
                options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
                options.JsonSerializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
            });
            return services;
        }

        public IServiceCollection AddAppAuthentication()
        {
            services.AddAuthorization();
            return services;
        }

        public IServiceCollection AddAppBusinessServices()
        {
            services.AddScoped<FlywheelService>();
            services.AddHostedService<MembershipReconciliationService>();
            return services;
        }
    }
}
