using System.ComponentModel.DataAnnotations;
using DysonNetwork.Shared.Auth;
using DysonNetwork.Shared.Capabilities;
using DysonNetwork.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using WattEngine.Flywheel.Flywheel;

namespace WattEngine.Flywheel.Admin;

/// <summary>
/// Platform-level Flywheel administration. Every action is gated by an
/// <see cref="AskPermissionAttribute"/> node checked against Padlock by
/// <c>RemotePermissionMiddleware</c>; workspace membership is not required.
/// Responses deliberately exclude encrypted bytes and storage keys.
/// </summary>
[ApiController]
[Route("/api/admin/flywheel")]
[Authorize]
[ApiFeature("admin.flywheel", Revision = 1)]
public class FlywheelAdminController(AppDatabase db, FlywheelService flywheel, FlywheelBlobStorage storage, IClock clock) : ControllerBase
{
    public class FlywheelStatsResponse
    {
        public Instant CalculatedAt { get; set; }
        public int DistinctWorkspaceCount { get; set; }
        public long TotalAppSettings { get; set; }
        public long TotalBlobs { get; set; }
        public long TotalBlobRevisions { get; set; }
        public long TotalBytes { get; set; }
        public long TotalAuditEntries { get; set; }
        public long AuditsLastDay { get; set; }
        public long AuditsLastWeek { get; set; }
        public long AuditsLastMonth { get; set; }
    }

    public class FlywheelAdminAppResponse
    {
        public Guid Id { get; set; }
        public Guid WorkspaceId { get; set; }
        public string AppId { get; set; } = string.Empty;
        public int RetainedRevisionCount { get; set; }
        public long EventCursor { get; set; }
        public int BlobCount { get; set; }
        public int RevisionCount { get; set; }
        public long TotalBytes { get; set; }
        public Instant UpdatedAt { get; set; }
    }

    public class UpdateAppSettingsAdminRequest
    {
        /// <summary>Admin override; not constrained by the workspace plan cap.</summary>
        [Range(0, 20)] public int RetainedRevisionCount { get; set; }
    }

    [HttpGet("stats")]
    [AskPermission(PermissionKeys.FlywheelView)]
    public async Task<ActionResult<FlywheelStatsResponse>> GetStats(CancellationToken ct)
    {
        var now = clock.GetCurrentInstant();
        var oneDayAgo = now - Duration.FromDays(1);
        var sevenDaysAgo = now - Duration.FromDays(7);
        var thirtyDaysAgo = now - Duration.FromDays(30);
        var audits = db.AuditEntries.AsNoTracking();

        return Ok(new FlywheelStatsResponse
        {
            CalculatedAt = now,
            DistinctWorkspaceCount = await db.Blobs.AsNoTracking().Select(x => x.WorkspaceId).Distinct().CountAsync(ct),
            TotalAppSettings = await db.AppSettings.AsNoTracking().LongCountAsync(ct),
            TotalBlobs = await db.Blobs.AsNoTracking().LongCountAsync(ct),
            TotalBlobRevisions = await db.BlobRevisions.AsNoTracking().LongCountAsync(ct),
            TotalBytes = await db.BlobRevisions.AsNoTracking().SumAsync(x => (long?)x.Size, ct) ?? 0,
            TotalAuditEntries = await audits.LongCountAsync(ct),
            AuditsLastDay = await audits.LongCountAsync(a => a.CreatedAt >= oneDayAgo, ct),
            AuditsLastWeek = await audits.LongCountAsync(a => a.CreatedAt >= sevenDaysAgo, ct),
            AuditsLastMonth = await audits.LongCountAsync(a => a.CreatedAt >= thirtyDaysAgo, ct)
        });
    }

    [HttpGet("apps")]
    [AskPermission(PermissionKeys.FlywheelView)]
    public async Task<ActionResult<List<FlywheelAdminAppResponse>>> ListApps(
        [FromQuery] Guid? workspaceId = null,
        [FromQuery] int take = 50,
        [FromQuery] int offset = 0,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);
        offset = Math.Max(offset, 0);

        var query = db.AppSettings.AsNoTracking();
        if (workspaceId.HasValue) query = query.Where(a => a.WorkspaceId == workspaceId.Value);

        var total = await query.CountAsync(ct);
        Response.Headers.Append("X-Total", total.ToString());

        var page = await query
            .OrderByDescending(a => a.UpdatedAt)
            .Skip(offset)
            .Take(take)
            .ToListAsync(ct);

        var result = new List<FlywheelAdminAppResponse>();
        foreach (var app in page)
        {
            var blobs = db.Blobs.AsNoTracking().Where(x => x.WorkspaceId == app.WorkspaceId && x.AppId == app.AppId);
            var blobIds = blobs.Select(x => x.Id);
            var revisions = db.BlobRevisions.AsNoTracking().Where(x => blobIds.Contains(x.BlobId));
            result.Add(new FlywheelAdminAppResponse
            {
                Id = app.Id,
                WorkspaceId = app.WorkspaceId,
                AppId = app.AppId,
                RetainedRevisionCount = app.RetainedRevisionCount,
                EventCursor = app.EventCursor,
                BlobCount = await blobs.CountAsync(ct),
                RevisionCount = await revisions.CountAsync(ct),
                TotalBytes = await revisions.SumAsync(x => (long?)x.Size, ct) ?? 0,
                UpdatedAt = app.UpdatedAt
            });
        }
        return result;
    }

    [HttpGet("audit")]
    [AskPermission(PermissionKeys.FlywheelAuditView)]
    public async Task<ActionResult<List<FlywheelAuditEntry>>> ListAudit(
        [FromQuery] Guid? workspaceId = null,
        [FromQuery] string? appId = null,
        [FromQuery] int take = 50,
        [FromQuery] int offset = 0,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 500);
        offset = Math.Max(offset, 0);

        var query = db.AuditEntries.AsNoTracking();
        if (workspaceId.HasValue) query = query.Where(a => a.WorkspaceId == workspaceId.Value);
        if (!string.IsNullOrWhiteSpace(appId)) query = query.Where(a => a.AppId == appId);

        var total = await query.CountAsync(ct);
        Response.Headers.Append("X-Total", total.ToString());

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip(offset)
            .Take(take)
            .ToListAsync(ct);
    }

    [HttpPatch("apps/{id:guid}")]
    [AskPermission(PermissionKeys.FlywheelAppsManage)]
    public async Task<ActionResult<FlywheelAppSettings>> UpdateAppSettings(Guid id, [FromBody] UpdateAppSettingsAdminRequest request, CancellationToken ct)
    {
        var settings = await db.AppSettings.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new FlywheelNotFoundException("App settings not found.");

        settings.RetainedRevisionCount = request.RetainedRevisionCount;
        settings.UpdatedAt = clock.GetCurrentInstant();
        await db.SaveChangesAsync(ct);
        return Ok(settings);
    }

    [HttpDelete("blobs/{blobId:guid}")]
    [AskPermission(PermissionKeys.FlywheelBlobsDelete)]
    public async Task<IActionResult> DeleteBlob(Guid blobId, [FromQuery] Guid workspaceId, [FromQuery] string appId, CancellationToken ct)
    {
        var blob = await db.Blobs.SingleOrDefaultAsync(
            x => x.WorkspaceId == workspaceId && x.AppId == appId && x.BlobId == blobId, ct)
            ?? throw new FlywheelNotFoundException("Blob not found.");

        var revisions = await db.BlobRevisions.Where(x => x.BlobId == blob.Id).ToListAsync(ct);
        foreach (var revision in revisions) await storage.DeleteAsync(revision.StorageKey, ct);

        db.BlobRevisions.RemoveRange(revisions);
        db.Blobs.Remove(blob);
        flywheel.AddAudit(workspaceId, appId, blobId, null, "blob.admin_deleted", CurrentUserId(), clock.GetCurrentInstant());
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private Guid CurrentUserId() => (HttpContext.Items["CurrentUser"] as SnAccount)?.Id ?? throw new FlywheelForbiddenException();
}
