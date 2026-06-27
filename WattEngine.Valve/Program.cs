using DysonNetwork.Shared.Auth;
using DysonNetwork.Shared.EventBus;
using DysonNetwork.Shared.Models;
using DysonNetwork.Shared.Networking;
using DysonNetwork.Shared.Queue;
using DysonNetwork.Shared.Registry;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WattEngine.Valve;
using WattEngine.Valve.Startup;
using WattEngine.Valve.Workspace;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Configure Kestrel and server options
builder.ConfigureAppKestrel(builder.Configuration, maxRequestBodySize: long.MaxValue);

// Add application services

builder.Services.AddAppServices(builder.Configuration);
builder.Services.AddAppAuthentication();
builder.Services.AddDysonAuth(builder.Configuration);
builder.Services.AddAccountService(builder.Configuration);

builder.Services.AddAppFlushHandlers();
builder.Services.AddAppBusinessServices(builder.Configuration);
builder.Services.AddAppScheduledJobs();

// Register payment event listener
builder.Services.AddEventBus()
    .AddListener<PaymentOrderEvent>(
        PaymentOrderEventBase.Type,
        async (evt, ctx) =>
        {
            if (evt.ProductIdentifier is not (WorkspacePlanPricing.ProductIdentifierPro or WorkspacePlanPricing.ProductIdentifierEnterprise))
                return;

            var logger = ctx.ServiceProvider.GetRequiredService<ILogger<Program>>();
            var ws = ctx.ServiceProvider.GetRequiredService<WorkspaceService>();

            var meta = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                JsonSerializer.Serialize(evt.Meta)
            );
            if (meta == null) return;

            if (!meta.TryGetValue("workspace_id", out var workspaceIdElem) ||
                !meta.TryGetValue("plan", out var planElem))
                return;

            var workspaceId = Guid.Parse(workspaceIdElem.GetString()!);
            var plan = (WorkspacePlan)planElem.GetInt32();

            try
            {
                await ws.ActivatePlan(workspaceId, evt.OrderId, plan);
                logger.LogInformation("Activated {Plan} plan for workspace {WorkspaceId}", plan, workspaceId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to activate plan for workspace {WorkspaceId}", workspaceId);
            }
        }
    );

builder.AddSwaggerManifest(
    "WattEngine.Valve",
    "Workspace and permission management for WattEngine services."
);

var app = builder.Build();

app.MapDefaultEndpoints();

// Run database migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDatabase>();
    await db.Database.MigrateAsync();
}

app.ConfigureAppMiddleware();

// Configure gRPC
app.ConfigureGrpcServices();

app.UseSwaggerManifest("WattEngine.Valve");

app.Run();
