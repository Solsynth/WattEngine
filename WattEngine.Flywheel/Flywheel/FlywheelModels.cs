using System.ComponentModel.DataAnnotations;
using NodaTime;

namespace WattEngine.Flywheel.Flywheel;

public class FlywheelAppSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    [MaxLength(1024)] public string AppId { get; set; } = string.Empty;
    public int RetainedRevisionCount { get; set; }
    public long EventCursor { get; set; }
    public Instant CreatedAt { get; set; }
    public Instant UpdatedAt { get; set; }
}

public class FlywheelBlob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    [MaxLength(1024)] public string AppId { get; set; } = string.Empty;
    public Guid BlobId { get; set; }
    public long CurrentRevision { get; set; }
    public long LastEventCursor { get; set; }
    public Instant CreatedAt { get; set; }
    public Instant UpdatedAt { get; set; }
}

public class FlywheelBlobRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BlobId { get; set; }
    public long Revision { get; set; }
    public int SchemeVersion { get; set; }
    [MaxLength(2048)] public string StorageKey { get; set; } = string.Empty;
    public long Size { get; set; }
    [MaxLength(128)] public string Sha256 { get; set; } = string.Empty;
    public Guid UploadedByAccountId { get; set; }
    public Instant CreatedAt { get; set; }
}

public record FlywheelSettingsResponse(int RetainedRevisionCount, int MaxRetainedRevisionCount, long EventCursor);
public record FlywheelBlobResponse(Guid BlobId, long CurrentRevision, long LastEventCursor, Instant UpdatedAt);
public record FlywheelRevisionResponse(Guid BlobId, long Revision, int SchemeVersion, long Size, string Sha256, Instant CreatedAt);
