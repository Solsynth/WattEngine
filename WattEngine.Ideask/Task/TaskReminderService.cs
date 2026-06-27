using Microsoft.EntityFrameworkCore;
using NodaTime;
using WattEngine.Ideask.Broad;
using WattEngine.Ideask.Connectivity;

namespace WattEngine.Ideask.Task;

public class TaskReminderService(
    AppDatabase db,
    RealtimeDeliveryService webSocketService,
    ILogger<TaskReminderService> logger,
    IClock clock
) : BackgroundService
{
    private readonly Duration[] _reminderIntervals =
    [
        Duration.FromDays(1),
        Duration.FromHours(12),
        Duration.FromHours(6),
        Duration.FromHours(3),
        Duration.FromHours(1),
        Duration.FromMinutes(30),
        Duration.FromMinutes(15),
        Duration.FromMinutes(5)
    ];

    private readonly string[] _reminderLabels =
    [
        "1d", "12h", "6h", "3h", "1h", "30m", "15m", "5m"
    ];

    protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndSendDueRemindersAsync(stoppingToken);

                await System.Threading.Tasks.Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in TaskDueReminderService");
                await System.Threading.Tasks.Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private async System.Threading.Tasks.Task CheckAndSendDueRemindersAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetCurrentInstant();

        var tasksNeedingReminders = await db.Tasks
            .Include(t => t.Broad)
            .Include(t => t.Assignees)
            .Where(t =>
                t.DeadlineAt.HasValue &&
                !t.CompletedAt.HasValue &&
                t.DeadlineAt.Value > now &&
                t.DeadlineAt.Value <= now + Duration.FromDays(1))
            .ToListAsync(cancellationToken);

        foreach (var task in tasksNeedingReminders)
        {
            if (!task.DeadlineAt.HasValue) continue;

            var timeUntilDue = task.DeadlineAt.Value - now;
            var broad = task.Broad;

            for (var i = 0; i < _reminderIntervals.Length; i++)
            {
                var interval = _reminderIntervals[i];
                var label = _reminderLabels[i];

                if (timeUntilDue > interval || timeUntilDue <= interval - Duration.FromMinutes(5)) continue;
                await SendDueReminderAsync(task, broad, timeUntilDue, label, cancellationToken);
                break;
            }
        }
    }

    private async System.Threading.Tasks.Task SendDueReminderAsync(
        WtTask task,
        WtBroad broad,
        Duration timeUntilDue,
        string reminderLabel,
        CancellationToken cancellationToken)
    {
        try
        {
            var userIds = new List<string>
            {
                broad.AccountId.ToString()
            };

            var assigneeIds = task.Assignees.Select(a => a.AccountId.ToString()).ToList();
            userIds.AddRange(assigneeIds);

            var packet = webSocketService.CreateTaskDueReminderPacket(
                task,
                broad,
                timeUntilDue,
                reminderLabel,
                Guid.Empty
            );

            await webSocketService.SendToUsersAsync(userIds.Distinct().ToList(), packet, cancellationToken);

            logger.LogInformation(
                "Sent due reminder for task {TaskId} ({TaskName}) - due in {TimeUntilDue} ({Label})",
                task.Id, task.Name, timeUntilDue, reminderLabel);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to send due reminder for task {TaskId} ({TaskName})",
                task.Id, task.Name);
        }
    }

    public override async System.Threading.Tasks.Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("TaskDueReminderService is stopping");
        await base.StopAsync(cancellationToken);
    }
}
