using DysonNetwork.Shared.Registry;
using NodaTime;
using WattEngine.Ideask.Broad;
using WattEngine.Ideask.Models.WebSocket;
using WattEngine.Ideask.Task;

namespace WattEngine.Ideask.Connectivity;

public class RealtimeDeliveryService(
    RemoteRingService ringService,
    ILogger<RealtimeDeliveryService> logger,
    IClock clock
)
{
    public async System.Threading.Tasks.Task SendToProjectMembersAsync(
        string projectId,
        IdeaskWebSocketPacket packet,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var webSocketPacket = packet.ToWebSocketPacket();
            var packetBytes = webSocketPacket.ToBytes();

            // Note: We'll need to get project member IDs from the database
            // This method will be called from services that have database access
            // The actual implementation will be in the specific services
            
            await ringService.PushWebSocketPacketToUsers(
                new List<string>(), // Will be filled by calling service
                webSocketPacket.Type,
                packetBytes
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send WebSocket packet for project {ProjectId}", projectId);
        }
    }

    public async System.Threading.Tasks.Task SendToUsersAsync(
        List<string> userIds,
        IdeaskWebSocketPacket packet,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var webSocketPacket = packet.ToWebSocketPacket();
            var packetBytes = webSocketPacket.ToBytes();

            await ringService.PushWebSocketPacketToUsers(
                userIds,
                webSocketPacket.Type,
                packetBytes
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send WebSocket packet to {UserCount} users", userIds.Count);
        }
    }

    public IdeaskWebSocketPacket CreateTaskAssignedPacket(
        WtTask task,
        WtBroad broad,
        WtProject? project,
        List<string> assignedUserIds,
        List<string> unassignedUserIds,
        Guid triggeredBy
    )
    {
        var payload = new TaskAssignedPayload(task, broad, project, assignedUserIds, unassignedUserIds);
        
        return new IdeaskWebSocketPacket(
            "task_assigned",
            "task",
            payload,
            clock.GetCurrentInstant(),
            triggeredBy
        );
    }

    public IdeaskWebSocketPacket CreateTaskDueReminderPacket(
        WtTask task,
        WtBroad broad,
        WtProject? project,
        Duration timeUntilDue,
        string reminderLevel,
        Guid triggeredBy
    )
    {
        var payload = new TaskDueReminderPayload(task, broad, project, timeUntilDue, reminderLevel);
        
        return new IdeaskWebSocketPacket(
            "task_due_reminder",
            "task",
            payload,
            clock.GetCurrentInstant(),
            triggeredBy
        );
    }

    public IdeaskWebSocketPacket CreateTaskCreatedPacket(
        WtTask task,
        WtBroad broad,
        WtProject? project,
        Guid triggeredBy
    )
    {
        var payload = new TaskCreatedPayload(task, broad, project);
        
        return new IdeaskWebSocketPacket(
            "task_created",
            "task",
            payload,
            clock.GetCurrentInstant(),
            triggeredBy
        );
    }

    public IdeaskWebSocketPacket CreateTaskUpdatedPacket(
        WtTask task,
        WtBroad broad,
        WtProject? project,
        List<string> changedProperties,
        Guid triggeredBy
    )
    {
        var payload = new TaskUpdatedPayload(task, broad, project, changedProperties);
        
        return new IdeaskWebSocketPacket(
            "task_updated",
            "task",
            payload,
            clock.GetCurrentInstant(),
            triggeredBy
        );
    }

    public IdeaskWebSocketPacket CreateProjectCreatedPacket(
        WtProject project,
        Guid triggeredBy
    )
    {
        var payload = new ProjectCreatedPayload(project);
        
        return new IdeaskWebSocketPacket(
            "project_created",
            "project",
            payload,
            clock.GetCurrentInstant(),
            triggeredBy
        );
    }

    public IdeaskWebSocketPacket CreateProjectUpdatedPacket(
        WtProject project,
        List<string> changedProperties,
        Guid triggeredBy
    )
    {
        var payload = new ProjectUpdatedPayload(project, changedProperties);
        
        return new IdeaskWebSocketPacket(
            "project_updated",
            "project",
            payload,
            clock.GetCurrentInstant(),
            triggeredBy
        );
    }

    public IdeaskWebSocketPacket CreateProjectMemberChangedPacket(
        WtProject project,
        List<string> addedMemberIds,
        List<string> removedMemberIds,
        Guid triggeredBy
    )
    {
        var payload = new ProjectMemberChangedPayload(project, addedMemberIds, removedMemberIds);
        
        return new IdeaskWebSocketPacket(
            "project_member_changed",
            "project",
            payload,
            clock.GetCurrentInstant(),
            triggeredBy
        );
    }

    public IdeaskWebSocketPacket CreateBroadCreatedPacket(
        WtBroad broad,
        WtProject? project,
        Guid triggeredBy
    )
    {
        var payload = new BroadCreatedPayload(broad, project);
        
        return new IdeaskWebSocketPacket(
            "broad_created",
            "broad",
            payload,
            clock.GetCurrentInstant(),
            triggeredBy
        );
    }

    public IdeaskWebSocketPacket CreateBroadUpdatedPacket(
        WtBroad broad,
        WtProject? project,
        List<string> changedProperties,
        Guid triggeredBy
    )
    {
        var payload = new BroadUpdatedPayload(broad, project, changedProperties);
        
        return new IdeaskWebSocketPacket(
            "broad_updated",
            "broad",
            payload,
            clock.GetCurrentInstant(),
            triggeredBy
        );
    }
}