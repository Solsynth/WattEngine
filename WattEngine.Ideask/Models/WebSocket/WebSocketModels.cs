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
                ["triggered_by"] = TriggeredBy
            }
        };
    }
}

public record TaskAssignedPayload(
    WtTask Task,
    WtBroad Broad,
    List<string> AssignedUserIds,
    List<string> UnassignedUserIds
);

public record TaskDueReminderPayload(
    WtTask Task,
    WtBroad Broad,
    Duration TimeUntilDue,
    string ReminderLevel
);

public record TaskCreatedPayload(
    WtTask Task,
    WtBroad Broad
);

public record TaskUpdatedPayload(
    WtTask Task,
    WtBroad Broad,
    List<string> ChangedProperties
);

public record BroadCreatedPayload(
    WtBroad Broad
);

public record BroadUpdatedPayload(
    WtBroad Broad,
    List<string> ChangedProperties
);
