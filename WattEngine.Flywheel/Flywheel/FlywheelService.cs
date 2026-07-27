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

    public async Task<FlywheelAppSettings> GetSettingsAsync(Guid workspaceId, string appId, Guid accountId, int requiredRole, CancellationToken ct)
    {
        if (!await workspaces.IsMemberWithRole(workspaceId, accountId, [requiredRole], ct))
            throw new FlywheelForbiddenException();
        if (await workspaces.GetPlan(workspaceId, ct) is not (DyWorkspacePlan.Pro or DyWorkspacePlan.Enterprise))
            throw new FlywheelSubscriptionRequiredException();

        var settings = await db.AppSettings.SingleOrDefaultAsync(x => x.WorkspaceId == workspaceId && x.AppId == appId, ct);
        if (settings is not null) return settings;
        var now = clock.GetCurrentInstant();
        settings = new FlywheelAppSettings { WorkspaceId = workspaceId, AppId = appId, CreatedAt = now, UpdatedAt = now };
        db.AppSettings.Add(settings);
        await db.SaveChangesAsync(ct);
        return settings;
    }

    public async Task<int> GetRetentionCapAsync(Guid workspaceId, CancellationToken ct)
    {
        var plan = await workspaces.GetPlan(workspaceId, ct);
        return plan switch { DyWorkspacePlan.Pro => 3, DyWorkspacePlan.Enterprise => 20, _ => 0 };
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
}

public class FlywheelForbiddenException : Exception;
public class FlywheelNotFoundException(string message) : Exception(message);
public class FlywheelConflictException(string message) : Exception(message);
public class FlywheelValidationException(string message) : Exception(message);
public class FlywheelSubscriptionRequiredException : Exception { public FlywheelSubscriptionRequiredException() : base("Flywheel requires a Pro or Enterprise workspace subscription.") { } }
