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

/// <summary>Metadata-only audit evidence. It never contains blob bytes, decrypted names, or S3 keys.</summary>
public class FlywheelAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    [MaxLength(1024)] public string AppId { get; set; } = string.Empty;
    public Guid? BlobId { get; set; }
    public long? Revision { get; set; }
    [MaxLength(64)] public string Action { get; set; } = string.Empty;
    public Guid ActorAccountId { get; set; }
    public Instant CreatedAt { get; set; }
}

public record FlywheelSettingsResponse(int RetainedRevisionCount, int MaxRetainedRevisionCount, long EventCursor);
public record FlywheelBlobResponse(Guid BlobId, long CurrentRevision, long LastEventCursor, Instant UpdatedAt);
public record FlywheelRevisionResponse(Guid BlobId, long Revision, int SchemeVersion, long Size, string Sha256, Instant CreatedAt);
public record FlywheelOwnerAppResponse(string AppId, int RetainedRevisionCount, int BlobCount, int RetainedRevisionCountTotal, long RetainedBytes, Instant LastUpdatedAt);
public record FlywheelOwnerBlobResponse(Guid BlobId, long CurrentRevision, int RetainedRevisionCount, long RetainedBytes, Instant UpdatedAt);
public record FlywheelStorageQuotaResponse(long UsedBytes, long BudgetBytes);
public record FlywheelAuditResponse(string AppId, Guid? BlobId, long? Revision, string Action, Guid ActorAccountId, Instant CreatedAt);
