using DysonNetwork.Shared.Registry;
using Microsoft.Extensions.Configuration;
using NodaTime;
using WattEngine.Ideask.Broad;
using WattEngine.Ideask.Models.WebSocket;
using WattEngine.Ideask.Task;

namespace WattEngine.Ideask.Connectivity;

public class RealtimeDeliveryService(
    RemoteRingService ringService,
    ILogger<RealtimeDeliveryService> logger,
    IClock clock,
    RemoteWorkspaceService workspaces,
    IConfiguration configuration
)
{
    /// <summary>
    /// Blade wsgateway namespace for SolWatt clients (`?namespace=`).
    /// Must match the client product tenant id (Ring app id / bundle id).
    /// </summary>
    private string WebsocketNamespace =>
        configuration["Realtime:WebsocketNamespace"]
        ?? configuration["SolWatt:AppId"]
        ?? "dev.solsynth.solarwatt";

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
            var ns = WebsocketNamespace;

            foreach (var userId in userIds)
            {
                await ringService.SendWebSocketPacketToUser(
                    userId,
                    webSocketPacket.Type,
                    packetBytes,
                    @namespace: ns
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send WebSocket packet to {UserCount} users", userIds.Count);
        }
    }

    public async System.Threading.Tasks.Task SendTaskPacketAsync(
        WtBroad broad,
        IEnumerable<string> recipientUserIds,
        IdeaskWebSocketPacket packet,
        CancellationToken cancellationToken = default
    )
    {
        var recipients = recipientUserIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (broad.WorkspaceId.HasValue)
        {
            try
            {
                recipients.UnionWith(await workspaces.GetActiveMemberAccountIds(broad.WorkspaceId.Value));
            }
            catch (Exception ex)
            {
                // Do not fail a task mutation merely because collaboration delivery cannot expand recipients.
                logger.LogWarning(ex, "Failed to load members of workspace {WorkspaceId} for task packet delivery", broad.WorkspaceId);
            }
        }

        await SendToUsersAsync(recipients.ToList(), packet, cancellationToken);
    }

    public IdeaskWebSocketPacket CreateTaskAssignedPacket(
        WtTask task,
        WtBroad broad,
        List<string> assignedUserIds,
        List<string> unassignedUserIds,
        Guid triggeredBy
    )
    {
        var payload = new TaskAssignedPayload(task, broad, assignedUserIds, unassignedUserIds);

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
        Duration timeUntilDue,
        string reminderLevel,
        Guid triggeredBy
    )
    {
        var payload = new TaskDueReminderPayload(task, broad, timeUntilDue, reminderLevel);

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
        Guid triggeredBy
    )
    {
        var payload = new TaskCreatedPayload(task, broad);

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
        List<string> changedProperties,
        Guid triggeredBy
    )
    {
        var payload = new TaskUpdatedPayload(task, broad, changedProperties);

        return new IdeaskWebSocketPacket(
            "task_updated",
            "task",
            payload,
            clock.GetCurrentInstant(),
            triggeredBy
        );
    }

    public IdeaskWebSocketPacket CreateBroadCreatedPacket(
        WtBroad broad,
        Guid triggeredBy
    )
    {
        var payload = new BroadCreatedPayload(broad);

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
        List<string> changedProperties,
        Guid triggeredBy
    )
    {
        var payload = new BroadUpdatedPayload(broad, changedProperties);

        return new IdeaskWebSocketPacket(
            "broad_updated",
            "broad",
            payload,
            clock.GetCurrentInstant(),
            triggeredBy
        );
    }
}
