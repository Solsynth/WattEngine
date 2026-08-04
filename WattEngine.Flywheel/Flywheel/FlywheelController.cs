using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using DysonNetwork.Shared.Models;
using DysonNetwork.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WattEngine.Flywheel.Flywheel;

[ApiController, Authorize]
[Route("/api/workspaces/{workspaceId:guid}/apps/{appId}")]
public partial class FlywheelController(AppDatabase db, FlywheelService flywheel, FlywheelBlobStorage storage) : ControllerBase
{
    public class UpdateSettingsRequest { [Range(0, 20)] public int RetainedRevisionCount { get; set; } }
    public class UploadBlobRequest { [Required] public IFormFile File { get; set; } = null!; [Range(1, int.MaxValue)] public int SchemeVersion { get; set; } [Range(0, long.MaxValue)] public long ExpectedRevision { get; set; } }

    [HttpGet("settings")]
    [AskPermission(PermissionKeys.FlywheelView)]
    public async Task<ActionResult<FlywheelSettingsResponse>> GetSettings(Guid workspaceId, string appId, CancellationToken ct) => ToSettings(await Settings(workspaceId, appId, FlywheelService.ViewerRole, ct), FlywheelService.MaxRetainedRevisionCount);

    [HttpPatch("settings")]
    [AskPermission(PermissionKeys.FlywheelAppsManage)]
    public async Task<ActionResult<FlywheelSettingsResponse>> UpdateSettings(Guid workspaceId, string appId, UpdateSettingsRequest request, CancellationToken ct)
    {
        var settings = await Settings(workspaceId, appId, FlywheelService.AdminRole, ct);
        settings.RetainedRevisionCount = request.RetainedRevisionCount;
        flywheel.AddAudit(workspaceId, appId, null, null, "app.retention-updated", CurrentUserId(), NodaTime.SystemClock.Instance.GetCurrentInstant());
        await db.SaveChangesAsync(ct);
        return ToSettings(settings, FlywheelService.MaxRetainedRevisionCount);
    }

    [HttpGet("blobs")]
    [AskPermission(PermissionKeys.FlywheelView)]
    public async Task<ActionResult<List<FlywheelBlobResponse>>> ListBlobs(Guid workspaceId, string appId, CancellationToken ct)
    {
        await Settings(workspaceId, appId, FlywheelService.ViewerRole, ct);
        return await db.Blobs.Where(x => x.WorkspaceId == workspaceId && x.AppId == appId).OrderByDescending(x => x.UpdatedAt).Select(x => ToBlob(x)).ToListAsync(ct);
    }

    [HttpGet("blobs/{blobId:guid}")]
    [AskPermission(PermissionKeys.FlywheelView)]
    public async Task<ActionResult<FlywheelBlobResponse>> GetBlob(Guid workspaceId, string appId, Guid blobId, CancellationToken ct)
    {
        await Settings(workspaceId, appId, FlywheelService.ViewerRole, ct);
        var blob = await FindBlob(workspaceId, appId, blobId, ct); return ToBlob(blob);
    }

    [HttpPut("blobs/{blobId:guid}")]
    [Consumes("multipart/form-data")]
    [AskPermission(PermissionKeys.FlywheelBlobsManage)]
    public async Task<ActionResult<FlywheelRevisionResponse>> Upload(Guid workspaceId, string appId, Guid blobId, [FromForm] UploadBlobRequest request, CancellationToken ct)
    {
        var settings = await Settings(workspaceId, appId, FlywheelService.MemberRole, ct);
        await flywheel.EnsureWithinStorageBudgetAsync(workspaceId, request.File.Length, ct);
        var newRevision = request.ExpectedRevision + 1;
        var key = storage.BuildObjectKey(workspaceId, appId, blobId, newRevision);
        var saved = await storage.SaveAsync(key, request.File, ct);
        try
        {
            var revision = await flywheel.CommitRevisionAsync(settings, blobId, request.ExpectedRevision, request.SchemeVersion, saved.Size, saved.Sha256, key, CurrentUserId(), ct);
            var blob = await FindBlob(workspaceId, appId, blobId, ct);
            foreach (var stale in await flywheel.TrimRevisionsAsync(blob, settings.RetainedRevisionCount, ct)) await storage.DeleteAsync(stale.StorageKey, ct);
            return CreatedAtAction(nameof(GetBlob), new { workspaceId, appId, blobId }, ToRevision(blobId, revision));
        }
        catch { await storage.DeleteAsync(key, ct); throw; }
    }

    [HttpGet("blobs/{blobId:guid}/revisions/{revision:long}")]
    [AskPermission(PermissionKeys.FlywheelView)]
    public async Task<ActionResult<FlywheelRevisionResponse>> GetRevision(Guid workspaceId, string appId, Guid blobId, long revision, CancellationToken ct)
    {
        await Settings(workspaceId, appId, FlywheelService.ViewerRole, ct);
        var blob = await FindBlob(workspaceId, appId, blobId, ct);
        var item = await db.BlobRevisions.SingleOrDefaultAsync(x => x.BlobId == blob.Id && x.Revision == revision, ct) ?? throw new FlywheelNotFoundException("Revision not found.");
        return ToRevision(blobId, item);
    }

    [HttpGet("blobs/{blobId:guid}/content")]
    [AskPermission(PermissionKeys.FlywheelView)]
    public async Task<IActionResult> Download(Guid workspaceId, string appId, Guid blobId, [FromQuery] long? revision, CancellationToken ct)
    {
        await Settings(workspaceId, appId, FlywheelService.ViewerRole, ct);
        var blob = await FindBlob(workspaceId, appId, blobId, ct);
        var number = revision ?? blob.CurrentRevision;
        var item = await db.BlobRevisions.SingleOrDefaultAsync(x => x.BlobId == blob.Id && x.Revision == number, ct) ?? throw new FlywheelNotFoundException("Revision not found.");
        var stream = await storage.OpenAsync(item.StorageKey, ct) ?? throw new FlywheelNotFoundException("Blob content is unavailable.");
        return File(stream, "application/octet-stream", enableRangeProcessing: true);
    }

    [HttpGet("events")]
    [AskPermission(PermissionKeys.FlywheelView)]
    public async Task Events(Guid workspaceId, string appId, [FromQuery] long after = 0, CancellationToken ct = default)
    {
        Response.Headers.ContentType = "text/event-stream"; Response.Headers.CacheControl = "no-cache";
        var cursor = after;
        while (!ct.IsCancellationRequested)
        {
            var settings = await Settings(workspaceId, appId, FlywheelService.ViewerRole, ct);
            var changed = await db.Blobs.Where(x => x.WorkspaceId == workspaceId && x.AppId == appId && x.LastEventCursor > cursor).OrderBy(x => x.LastEventCursor).Select(x => new { x.BlobId, x.CurrentRevision, x.LastEventCursor }).ToListAsync(ct);
            foreach (var item in changed) { cursor = item.LastEventCursor; await Response.WriteAsync($"id: {cursor}\nevent: blob-updated\ndata: {{\"blob_id\":\"{item.BlobId}\",\"revision\":{item.CurrentRevision}}}\n\n", ct); }
            if (changed.Count == 0) await Response.WriteAsync(": keep-alive\n\n", ct);
            await Response.Body.FlushAsync(ct); await Task.Delay(TimeSpan.FromSeconds(15), ct);
        }
    }

    private async Task<FlywheelAppSettings> Settings(Guid workspaceId, string appId, int role, CancellationToken ct) { if (!AppIdRegex().IsMatch(appId)) throw new FlywheelValidationException("app_id must be a reverse-DNS package identifier."); return await flywheel.GetSettingsAsync(workspaceId, appId, CurrentUserId(), role, ct); }
    private async Task<FlywheelBlob> FindBlob(Guid workspaceId, string appId, Guid blobId, CancellationToken ct) => await db.Blobs.SingleOrDefaultAsync(x => x.WorkspaceId == workspaceId && x.AppId == appId && x.BlobId == blobId, ct) ?? throw new FlywheelNotFoundException("Blob not found.");
    private Guid CurrentUserId() => (HttpContext.Items["CurrentUser"] as SnAccount)?.Id ?? throw new FlywheelForbiddenException();
    private static FlywheelSettingsResponse ToSettings(FlywheelAppSettings x, int cap) => new(x.RetainedRevisionCount, cap, x.EventCursor);
    private static FlywheelBlobResponse ToBlob(FlywheelBlob x) => new(x.BlobId, x.CurrentRevision, x.LastEventCursor, x.UpdatedAt);
    private static FlywheelRevisionResponse ToRevision(Guid blobId, FlywheelBlobRevision x) => new(blobId, x.Revision, x.SchemeVersion, x.Size, x.Sha256, x.CreatedAt);
    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?(?:\\.[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)+$", RegexOptions.CultureInvariant)] private static partial Regex AppIdRegex();
}
