using DysonNetwork.Shared.Proto;
using DysonNetwork.Shared.Registry;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace WattEngine.Flywheel.Flywheel;

public class FlywheelService(AppDatabase db, RemoteWorkspaceService workspaces, IClock clock)
{
    public const int ViewerRole = 25;
    public const int MemberRole = 50;
    public const int AdminRole = 75;
    public const int OwnerRole = 100;

    public async Task<FlywheelAppSettings> GetSettingsAsync(Guid workspaceId, string appId, Guid accountId, int requiredRole, CancellationToken ct)
    {
        if (!await workspaces.IsMemberWithRole(workspaceId, accountId, [requiredRole], ct))
            throw new FlywheelForbiddenException();

        var settings = await db.AppSettings.SingleOrDefaultAsync(x => x.WorkspaceId == workspaceId && x.AppId == appId, ct);
        if (settings is not null) return settings;
        var now = clock.GetCurrentInstant();
        settings = new FlywheelAppSettings { WorkspaceId = workspaceId, AppId = appId, CreatedAt = now, UpdatedAt = now };
        db.AppSettings.Add(settings);
        await db.SaveChangesAsync(ct);
        return settings;
    }

    public const int MaxRetainedRevisionCount = 20;

    /// <summary>Flywheel blobs may use up to half of the workspace's plan storage quota.</summary>
    public async Task<long> GetStorageBudgetAsync(Guid workspaceId, CancellationToken ct)
    {
        var workspace = await workspaces.GetWorkspace(workspaceId, ct);
        var quota = await workspaces.GetPlanQuota(workspace.Plan, ct);
        return quota.MaxStorageBytes / 2;
    }

    public async Task EnsureWithinStorageBudgetAsync(Guid workspaceId, long additionalBytes, CancellationToken ct)
    {
        var budget = await GetStorageBudgetAsync(workspaceId, ct);
        var blobIds = db.Blobs.Where(x => x.WorkspaceId == workspaceId).Select(x => x.Id);
        var used = await db.BlobRevisions.Where(x => blobIds.Contains(x.BlobId)).SumAsync(x => (long?)x.Size, ct) ?? 0;
        if (used + additionalBytes > budget)
            throw new FlywheelStorageQuotaExceededException($"Flywheel blob storage would exceed {budget:N0} bytes (50% of this workspace's storage quota).");
    }

    public async Task<FlywheelBlobRevision> CommitRevisionAsync(
        FlywheelAppSettings settings, Guid blobId, long expectedRevision, int schemeVersion, long size, string sha256, string storageKey, Guid accountId, CancellationToken ct)
    {
        var blob = await db.Blobs.SingleOrDefaultAsync(x => x.WorkspaceId == settings.WorkspaceId && x.AppId == settings.AppId && x.BlobId == blobId, ct);
        if (blob is null)
        {
            if (expectedRevision != 0) throw new FlywheelConflictException("The blob does not exist; use expected_revision=0 to create it.");
            blob = new FlywheelBlob { WorkspaceId = settings.WorkspaceId, AppId = settings.AppId, BlobId = blobId, CreatedAt = clock.GetCurrentInstant() };
            db.Blobs.Add(blob);
        }
        else if (blob.CurrentRevision != expectedRevision)
            throw new FlywheelConflictException($"The blob has revision {blob.CurrentRevision}; re-download it before uploading.");

        var now = clock.GetCurrentInstant();
        var revision = new FlywheelBlobRevision { BlobId = blob.Id, Revision = blob.CurrentRevision + 1, SchemeVersion = schemeVersion, StorageKey = storageKey, Size = size, Sha256 = sha256, UploadedByAccountId = accountId, CreatedAt = now };
        settings.EventCursor++;
        settings.UpdatedAt = now;
        blob.CurrentRevision = revision.Revision;
        blob.LastEventCursor = settings.EventCursor;
        blob.UpdatedAt = now;
        db.BlobRevisions.Add(revision);
        AddAudit(settings.WorkspaceId, settings.AppId, blobId, revision.Revision, "blob.uploaded", accountId, now);
        await db.SaveChangesAsync(ct);
        return revision;
    }

    public async Task<List<FlywheelBlobRevision>> TrimRevisionsAsync(FlywheelBlob blob, int retainedPriorCount, CancellationToken ct)
    {
        var keep = retainedPriorCount + 1;
        var stale = await db.BlobRevisions.Where(x => x.BlobId == blob.Id).OrderByDescending(x => x.Revision).Skip(keep).ToListAsync(ct);
        if (stale.Count > 0) { db.BlobRevisions.RemoveRange(stale); await db.SaveChangesAsync(ct); }
        return stale;
    }

    public async Task RequireOwnerAsync(Guid workspaceId, Guid accountId, CancellationToken ct)
    {
        if (!await workspaces.IsMemberWithRole(workspaceId, accountId, [OwnerRole], ct))
            throw new FlywheelForbiddenException();
    }

    public void AddAudit(Guid workspaceId, string appId, Guid? blobId, long? revision, string action, Guid actorAccountId, Instant at) =>
        db.AuditEntries.Add(new FlywheelAuditEntry { WorkspaceId = workspaceId, AppId = appId, BlobId = blobId, Revision = revision, Action = action, ActorAccountId = actorAccountId, CreatedAt = at });
}

public class FlywheelForbiddenException : Exception;
public class FlywheelNotFoundException(string message) : Exception(message);
public class FlywheelConflictException(string message) : Exception(message);
public class FlywheelValidationException(string message) : Exception(message);
public class FlywheelStorageQuotaExceededException(string message) : Exception(message);
