using DysonNetwork.Shared.Auth;
using DysonNetwork.Shared.Networking;
using DysonNetwork.Shared.Registry;
using Microsoft.EntityFrameworkCore;
using WattEngine.Ideask;
using WattEngine.Ideask.Startup;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Configure Kestrel and server options
builder.ConfigureAppKestrel(builder.Configuration, maxRequestBodySize: long.MaxValue);

// Add application services

builder.Services.AddAppServices(builder.Configuration);
builder.Services.AddAppAuthentication();
builder.Services.AddDysonAuth();
builder.Services.AddRingService();
builder.Services.AddAccountService();

builder.Services.AddAppFlushHandlers();
builder.Services.AddAppBusinessServices(builder.Configuration);
builder.Services.AddAppScheduledJobs();

builder.AddSwaggerManifest(
    "WattEngine.Ideask",
    "The todo app in the SolarWatt."
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

app.UseSwaggerManifest("DysonNetwork.Drive");

app.Run();
