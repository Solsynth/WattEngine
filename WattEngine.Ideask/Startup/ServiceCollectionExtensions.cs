using System.Text.Json;
using System.Text.Json.Serialization;
using DysonNetwork.Shared.Cache;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using WattEngine.Ideask.Connectivity;
using WattEngine.Ideask.Task;

namespace WattEngine.Ideask.Startup;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAppServices(IConfiguration configuration)
        {
            services.AddDbContext<AppDatabase>();
            services.AddHttpContextAccessor();

            services.AddHttpClient();

            // Register gRPC services
            services.AddGrpc(options =>
            {
                options.EnableDetailedErrors = true; // Will be adjusted in Program.cs
                options.MaxReceiveMessageSize = 16 * 1024 * 1024; // 16MB
                options.MaxSendMessageSize = 16 * 1024 * 1024; // 16MB
            });
            services.AddGrpcReflection();

            services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals;
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

        public IServiceCollection AddAppFlushHandlers()
        {
            services.AddSingleton<FlushBufferService>();

            return services;
        }

        public IServiceCollection AddAppBusinessServices(IConfiguration configuration)
        {
            services.AddScoped<RealtimeDeliveryService>();
            services.AddHostedService<TaskReminderService>();

            return services;
        }
    }
}
