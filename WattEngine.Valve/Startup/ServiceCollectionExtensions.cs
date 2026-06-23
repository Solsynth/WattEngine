using System.Text.Json;
using System.Text.Json.Serialization;
using DysonNetwork.Shared.Cache;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using WattEngine.Valve.Workspace;

namespace WattEngine.Valve.Startup;

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
                options.EnableDetailedErrors = true;
                options.MaxReceiveMessageSize = 16 * 1024 * 1024;
                options.MaxSendMessageSize = 16 * 1024 * 1024;
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
            services.AddScoped<WorkspaceService>();
            services.AddScoped<PermissionService>();

            return services;
        }
    }
}
