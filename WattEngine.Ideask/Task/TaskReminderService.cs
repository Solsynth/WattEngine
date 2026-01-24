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

                // Check every 5 minutes to catch all reminder intervals
                await System.Threading.Tasks.Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation, exit gracefully
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in TaskDueReminderService");

                // Wait before retrying to avoid rapid-fire failures
                await System.Threading.Tasks.Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private async System.Threading.Tasks.Task CheckAndSendDueRemindersAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetCurrentInstant();

        // Get tasks with deadlines in the next 24 hours that are not completed
        var tasksNeedingReminders = await db.Tasks
            .Include(t => t.Broad)
            .ThenInclude(b => b.Project)
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

            // Check which reminder interval we're at
            for (var i = 0; i < _reminderIntervals.Length; i++)
            {
                var interval = _reminderIntervals[i];
                var label = _reminderLabels[i];

                // Check if we're within this reminder window
                if (timeUntilDue > interval || timeUntilDue <= interval - Duration.FromMinutes(5)) continue;
                await SendDueReminderAsync(task, broad, broad.Project, timeUntilDue, label, cancellationToken);
                break; // Only send one reminder per check
            }
        }
    }

    private async System.Threading.Tasks.Task SendDueReminderAsync(
        WtTask task,
        WtBroad broad,
        WtProject? project,
        Duration timeUntilDue,
        string reminderLabel,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get all users who should receive the reminder
            var userIds = new List<string>
            {
                // Add broad creator
                broad.AccountId.ToString()
            };

            // Add project members if applicable
            if (project != null)
            {
                var projectMembers = await db.ProjectMembers
                    .Where(pm => pm.ProjectId == project.Id)
                    .Select(pm => pm.AccountId.ToString())
                    .ToListAsync(cancellationToken);
                userIds.AddRange(projectMembers);
            }

            // Add task assignees
            var assigneeIds = task.Assignees.Select(a => a.AccountId.ToString()).ToList();
            userIds.AddRange(assigneeIds);

            // Create and send the WebSocket packet
            var packet = webSocketService.CreateTaskDueReminderPacket(
                task,
                broad,
                project,
                timeUntilDue,
                reminderLabel,
                Guid.Empty // System-triggered, no specific user
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