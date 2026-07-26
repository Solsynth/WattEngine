using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace WattEngine.Flywheel.Flywheel;

public class FlywheelStream
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    [MaxLength(1024)] public string AppId { get; set; } = string.Empty;
    [MaxLength(1400)] public string MlsGroupId { get; set; } = string.Empty;
    public long CurrentCursor { get; set; }
    public long MlsEpoch { get; set; }
    public bool RequiresMlsRotation { get; set; }
    public Instant CreatedAt { get; set; }
    public Instant UpdatedAt { get; set; }
}

public class FlywheelDevice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StreamId { get; set; }
    public Guid AccountId { get; set; }
    [MaxLength(512)] public string DeviceId { get; set; } = string.Empty;
    [MaxLength(1024)] public string? Label { get; set; }
    public bool IsRevoked { get; set; }
    public long LastAcknowledgedCursor { get; set; }
    public Instant? LastSeenAt { get; set; }
    public Instant CreatedAt { get; set; }
    public Instant UpdatedAt { get; set; }
}

public class FlywheelOperation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StreamId { get; set; }
    public Guid DeviceRegistrationId { get; set; }
    public Guid OperationId { get; set; }
    public int SchemeVersion { get; set; }
    public long Cursor { get; set; }
    public byte[] Ciphertext { get; set; } = [];
    public Instant CreatedAt { get; set; }
    public Instant RetainUntil { get; set; }
}

public class FlywheelStreamMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StreamId { get; set; }
    public Guid AccountId { get; set; }
    public Instant ObservedAt { get; set; }
}

public record FlywheelStreamResponse(Guid WorkspaceId, string AppId, string MlsGroupId, long Cursor, long MlsEpoch, bool RequiresMlsRotation);
public record FlywheelDeviceResponse(Guid Id, string DeviceId, string? Label, bool IsRevoked, long LastAcknowledgedCursor, Instant? LastSeenAt);
public record FlywheelOperationResponse(Guid OperationId, string DeviceId, int SchemeVersion, long Cursor, byte[] Ciphertext, Instant CreatedAt);
