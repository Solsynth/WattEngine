using NodaTime;
using WattEngine.Ideask.Broad;
using WattEngine.Ideask.Task;

namespace WattEngine.Ideask.Models.WebSocket;

public record IdeaskWebSocketPacket(
    string Type,
    string Entity,
    object Data,
    Instant Timestamp,
    Guid TriggeredBy
)
{
    public DysonNetwork.Shared.Models.WebSocketPacket ToWebSocketPacket()
    {
        return new DysonNetwork.Shared.Models.WebSocketPacket
        {
            Type = $"ideask.{Type}",
            Data = new Dictionary<string, object>
            {
                ["entity"] = Entity,
                ["data"] = Data,
                ["timestamp"] = Timestamp,
                ["triggered_by "] = TriggeredBy
            }
        };
    }
}

public record TaskAssignedPayload(
    WtTask Task,
    WtBroad Broad,
    WtProject? Project,
    List<string> AssignedUserIds,
    List<string> UnassignedUserIds
);

public record TaskDueReminderPayload(
    WtTask Task,
    WtBroad Broad,
    WtProject? Project,
    Duration TimeUntilDue,
    string ReminderLevel // "1d", "12h", "6h", "3h", "1h", "30m", "15m", "5m"
);

public record TaskCreatedPayload(
    WtTask Task,
    WtBroad Broad,
    WtProject? Project
);

public record TaskUpdatedPayload(
    WtTask Task,
    WtBroad Broad,
    WtProject? Project,
    List<string> ChangedProperties
);

public record ProjectCreatedPayload(
    WtProject Project
);

public record ProjectUpdatedPayload(
    WtProject Project,
    List<string> ChangedProperties
);

public record ProjectMemberChangedPayload(
    WtProject Project,
    List<string> AddedMemberIds,
    List<string> RemovedMemberIds
);

public record BroadCreatedPayload(
    WtBroad Broad,
    WtProject? Project
);

public record BroadUpdatedPayload(
    WtBroad Broad,
    WtProject? Project,
    List<string> ChangedProperties
);