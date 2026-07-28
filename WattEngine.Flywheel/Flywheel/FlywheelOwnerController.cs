using DysonNetwork.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace WattEngine.Flywheel.Flywheel;

/// <summary>Workspace-owner management APIs. All responses deliberately exclude encrypted bytes and storage keys.</summary>
[ApiController, Authorize]
[Route("/api/workspaces/{workspaceId:guid}/flywheel")]
public class FlywheelOwnerController(AppDatabase db, FlywheelService flywheel, FlywheelBlobStorage storage, IClock clock) : ControllerBase
{
    [HttpGet("apps")]
    public async Task<ActionResult<List<FlywheelOwnerAppResponse>>> ListApps(Guid workspaceId, CancellationToken ct)
    {
        await Owner(workspaceId, ct);
        var apps = await db.AppSettings.Where(x => x.WorkspaceId == workspaceId).ToListAsync(ct);
        var result = new List<FlywheelOwnerAppResponse>();
        foreach (var app in apps)
        {
            var blobs = db.Blobs.Where(x => x.WorkspaceId == workspaceId && x.AppId == app.AppId);
            var blobIds = blobs.Select(x => x.Id);
            var revisions = db.BlobRevisions.Where(x => blobIds.Contains(x.BlobId));
            result.Add(new(app.AppId, app.RetainedRevisionCount, await blobs.CountAsync(ct), await revisions.CountAsync(ct), await revisions.SumAsync(x => (long?)x.Size, ct) ?? 0, app.UpdatedAt));
        }
        return result.OrderBy(x => x.AppId).ToList();
    }

    [HttpGet("apps/{appId}/blobs")]
    public async Task<ActionResult<List<FlywheelOwnerBlobResponse>>> ListAppBlobs(Guid workspaceId, string appId, CancellationToken ct)
    {
        await Owner(workspaceId, ct);
        var blobs = await db.Blobs.Where(x => x.WorkspaceId == workspaceId && x.AppId == appId).OrderByDescending(x => x.UpdatedAt).ToListAsync(ct);
        var result = new List<FlywheelOwnerBlobResponse>();
        foreach (var blob in blobs)
        {
            var revisions = db.BlobRevisions.Where(x => x.BlobId == blob.Id);
            result.Add(new(blob.BlobId, blob.CurrentRevision, await revisions.CountAsync(ct), await revisions.SumAsync(x => (long?)x.Size, ct) ?? 0, blob.UpdatedAt));
        }
        return result;
    }

    [HttpGet("apps/{appId}/audit")]
    public async Task<ActionResult<List<FlywheelAuditResponse>>> GetAudit(Guid workspaceId, string appId, [FromQuery] int take = 100, CancellationToken ct = default)
    {
        await Owner(workspaceId, ct);
        take = Math.Clamp(take, 1, 500);
        return await db.AuditEntries.Where(x => x.WorkspaceId == workspaceId && x.AppId == appId).OrderByDescending(x => x.CreatedAt).Take(take)
            .Select(x => new FlywheelAuditResponse(x.AppId, x.BlobId, x.Revision, x.Action, x.ActorAccountId, x.CreatedAt)).ToListAsync(ct);
    }

    [HttpDelete("apps/{appId}/blobs/{blobId:guid}")]
    public async Task<IActionResult> DeleteBlob(Guid workspaceId, string appId, Guid blobId, CancellationToken ct)
    {
        var actor = await Owner(workspaceId, ct);
        var blob = await db.Blobs.SingleOrDefaultAsync(x => x.WorkspaceId == workspaceId && x.AppId == appId && x.BlobId == blobId, ct) ?? throw new FlywheelNotFoundException("Blob not found.");
        var revisions = await db.BlobRevisions.Where(x => x.BlobId == blob.Id).ToListAsync(ct);
        foreach (var revision in revisions) await storage.DeleteAsync(revision.StorageKey, ct);
        db.BlobRevisions.RemoveRange(revisions);
        db.Blobs.Remove(blob);
        flywheel.AddAudit(workspaceId, appId, blobId, null, "blob.deleted", actor, clock.GetCurrentInstant());
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<Guid> Owner(Guid workspaceId, CancellationToken ct)
    {
        var accountId = (HttpContext.Items["CurrentUser"] as SnAccount)?.Id ?? throw new FlywheelForbiddenException();
        await flywheel.RequireOwnerAsync(workspaceId, accountId, ct);
        return accountId;
    }
}
