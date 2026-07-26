using Microsoft.EntityFrameworkCore;

namespace WattEngine.Flywheel.Flywheel;

/// <summary>
/// Detects Valve membership removals even while a Flywheel stream is idle.
/// A removal blocks new writes until the clients commit a new MLS epoch.
/// </summary>
public class MembershipReconciliationService(IServiceScopeFactory scopeFactory, ILogger<MembershipReconciliationService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDatabase>();
                var flywheel = scope.ServiceProvider.GetRequiredService<FlywheelService>();
                var streams = await db.Streams.ToListAsync(stoppingToken);
                foreach (var stream in streams)
                    await flywheel.RefreshMembershipAsync(stream, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Flywheel membership reconciliation failed.");
            }
        }
    }
}
