using DysonNetwork.Shared.Auth;
using DysonNetwork.Shared.Capabilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using WattEngine.Valve.Workspace;

namespace WattEngine.Valve.Admin;

[ApiController]
[Route("/api/admin/stats")]
[Authorize]
[ApiFeature("admin.stats", Revision = 1)]
public class WorkspaceStatsAdminController(AppDatabase db, IClock clock) : ControllerBase
{
    public class WorkspaceStatsResponse
    {
        public Instant CalculatedAt { get; set; }
        public long TotalWorkspaces { get; set; }
        public long TotalDeletedWorkspaces { get; set; }
        /// <summary>Keys are lowercase <see cref="WorkspaceType"/> names.</summary>
        public Dictionary<string, long> WorkspacesByType { get; set; } = [];
        /// <summary>Keys are lowercase <see cref="WorkspacePlan"/> names.</summary>
        public Dictionary<string, long> WorkspacesByPlan { get; set; } = [];
        public long TotalMembers { get; set; }
        public long TotalRolePermissionConfigs { get; set; }
        public long TotalUserPermissionOverrides { get; set; }
        public long TotalBundledPlans { get; set; }
    }

    [HttpGet]
    [AskPermission(PermissionKeys.WorkspacesView)]
    public async Task<ActionResult<WorkspaceStatsResponse>> GetStats(CancellationToken ct)
    {
        var workspaces = db.Workspaces.AsNoTracking();
        var byType = await workspaces
            .GroupBy(w => w.Type)
            .Select(g => new { Type = g.Key, Count = g.LongCount() })
            .ToDictionaryAsync(x => x.Type.ToString().ToLowerInvariant(), x => x.Count, ct);
        var byPlan = await workspaces
            .GroupBy(w => w.Plan)
            .Select(g => new { Plan = g.Key, Count = g.LongCount() })
            .ToDictionaryAsync(x => x.Plan.ToString().ToLowerInvariant(), x => x.Count, ct);

        return Ok(new WorkspaceStatsResponse
        {
            CalculatedAt = clock.GetCurrentInstant(),
            TotalWorkspaces = await workspaces.LongCountAsync(ct),
            TotalDeletedWorkspaces = await db.Workspaces.AsNoTracking().IgnoreQueryFilters()
                .LongCountAsync(w => w.DeletedAt != null, ct),
            WorkspacesByType = byType,
            WorkspacesByPlan = byPlan,
            TotalMembers = await db.WorkspaceMembers.AsNoTracking()
                .LongCountAsync(m => m.LeaveAt == null, ct),
            TotalRolePermissionConfigs = await db.WorkspaceRolePermissions.AsNoTracking().LongCountAsync(ct),
            TotalUserPermissionOverrides = await db.WorkspaceUserPermissions.AsNoTracking().LongCountAsync(ct),
            TotalBundledPlans = await db.WorkspaceBundledPlans.AsNoTracking().LongCountAsync(ct)
        });
    }
}
