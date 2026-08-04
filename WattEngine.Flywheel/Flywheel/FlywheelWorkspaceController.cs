using DysonNetwork.Shared.Auth;
using DysonNetwork.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WattEngine.Flywheel.Flywheel;

/// <summary>Workspace-scoped Flywheel endpoints.</summary>
[ApiController, Authorize]
[Route("/api/workspaces/{workspaceId:guid}")]
public class FlywheelWorkspaceController(AppDatabase db, FlywheelService flywheel) : ControllerBase
{
    [HttpGet("quota")]
    [AskPermission(PermissionKeys.FlywheelView)]
    public async Task<ActionResult<FlywheelStorageQuotaResponse>> GetStorageQuota(Guid workspaceId, CancellationToken ct)
    {
        var accountId = (HttpContext.Items["CurrentUser"] as SnAccount)?.Id ?? throw new FlywheelForbiddenException();
        await flywheel.RequireOwnerAsync(workspaceId, accountId, ct);
        var budget = await flywheel.GetStorageBudgetAsync(workspaceId, ct);
        var blobIds = db.Blobs.Where(x => x.WorkspaceId == workspaceId).Select(x => x.Id);
        var used = await db.BlobRevisions.Where(x => blobIds.Contains(x.BlobId)).SumAsync(x => (long?)x.Size, ct) ?? 0;
        return new FlywheelStorageQuotaResponse(used, budget);
    }
}
