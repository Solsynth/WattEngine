using DysonNetwork.Shared.Auth;
using DysonNetwork.Shared.Networking;
using DysonNetwork.Shared.Registry;
using Microsoft.EntityFrameworkCore;
using WattEngine.Flywheel;
using WattEngine.Flywheel.Startup;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.ConfigureAppKestrel(builder.Configuration, maxRequestBodySize: long.MaxValue);

builder.Services.AddAppServices(builder.Configuration);
builder.Services.AddAppAuthentication();
builder.Services.AddDysonAuth();
builder.Services.AddDyAuthModelProjection();
builder.Services.AddWorkspaceService();
builder.Services.AddAppBusinessServices();

builder.AddSwaggerManifest(
    "WattEngine.Flywheel",
    "End-to-end encrypted, package-scoped cross-device state transport."
);

var app = builder.Build();
app.MapDefaultEndpoints();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDatabase>();
    await db.Database.MigrateAsync();
}
app.ConfigureAppMiddleware();
app.UseSwaggerManifest("WattEngine.Flywheel");
app.Run();
