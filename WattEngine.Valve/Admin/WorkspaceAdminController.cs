using System.ComponentModel.DataAnnotations;
using DysonNetwork.Shared.Auth;
using DysonNetwork.Shared.Capabilities;
using DysonNetwork.Shared.Networking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using WattEngine.Valve.Workspace;

namespace WattEngine.Valve.Admin;

/// <summary>
/// Platform-level workspace administration. Every action is gated by an
/// <see cref="AskPermissionAttribute"/> node checked against Padlock by
/// <c>RemotePermissionMiddleware</c>; workspace membership is not required.
/// </summary>
[ApiController]
[Route("/api/admin/workspaces")]
[Authorize]
[ApiFeature("admin.workspaces", Revision = 1)]
public class WorkspaceAdminController(AppDatabase db, IClock clock, WorkspaceService workspace) : ControllerBase
{
    public class WorkspaceAdminSummary
    {
        public Guid Id { get; set; }
        public string Slug { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public WorkspaceType Type { get; set; }
        public WorkspacePlan Plan { get; set; }
        public Guid OwnerAccountId { get; set; }
        public bool IsBundled { get; set; }
        public int MemberCount { get; set; }
        public Instant? DeletedAt { get; set; }
        public Instant CreatedAt { get; set; }
        public Instant UpdatedAt { get; set; }
    }

    public class WorkspaceAdminDetailResponse
    {
        public WtWorkspace Workspace { get; set; } = null!;
        public List<WtWorkspaceMember> Members { get; set; } = [];
        public List<WtWorkspaceRolePermission> RolePermissions { get; set; } = [];
        public List<WtWorkspaceUserPermission> UserPermissions { get; set; } = [];
        public List<WtWorkspaceBundledPlan> BundledPlans { get; set; } = [];
    }

    public class UpdateWorkspaceRequest
    {
        [MaxLength(1024)] public string? Name { get; set; }
        [MaxLength(1024)] public string? Slug { get; set; }
        [MaxLength(4096)] public string? Description { get; set; }
    }

    public class UpdateWorkspacePlanRequest
    {
        [Required] public WorkspacePlan Plan { get; set; }
        public Instant? PlanExpiresAt { get; set; }
        public bool? IsBundled { get; set; }
    }

    public class BackfillWorkspacesRequest
    {
        [Required, MinLength(1), MaxLength(200)]
        public List<Guid> AccountIds { get; set; } = [];
    }

    /// <summary>
    /// Platform-admin backfill: for each account that doesn't yet own an individual workspace,
    /// create one (resolving the nick from the account profile). Existing or blocked accounts
    /// are reported per-account instead of failing the batch.
    /// </summary>
    [HttpPost("backfill")]
    [AskPermission(PermissionKeys.AdminWorkspacesManage)]
    public async Task<ActionResult<List<WorkspaceService.BackfillIndividualWorkspaceResult>>> BackfillWorkspaces(
        [FromBody] BackfillWorkspacesRequest request,
        CancellationToken ct = default)
    {
        var results = new List<WorkspaceService.BackfillIndividualWorkspaceResult>(request.AccountIds.Count);
        foreach (var accountId in request.AccountIds.Distinct())
            results.Add(await workspace.BackfillIndividualWorkspace(accountId));
        return Ok(results);
    }

    [HttpGet]
    [AskPermission(PermissionKeys.AdminWorkspacesView)]
    public async Task<ActionResult<List<WorkspaceAdminSummary>>> ListWorkspaces(
        [FromQuery] WorkspaceType? type = null,
        [FromQuery] WorkspacePlan? plan = null,
        [FromQuery] string? q = null,
        [FromQuery] bool includeDeleted = false,
        [FromQuery] int take = 50,
        [FromQuery] int offset = 0,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);
        offset = Math.Max(offset, 0);

        var query = db.Workspaces.AsNoTracking();
        if (includeDeleted)
            query = query.IgnoreQueryFilters();
        else
            query = query.Where(w => w.DeletedAt == null);

        if (type.HasValue) query = query.Where(w => w.Type == type.Value);
        if (plan.HasValue) query = query.Where(w => w.Plan == plan.Value);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var probe = q.Trim();
            query = query.Where(w =>
                EF.Functions.ILike(w.Slug, $"%{probe}%") ||
                EF.Functions.ILike(w.Name, $"%{probe}%"));
        }

        var total = await query.CountAsync(ct);
        Response.Headers.Append("X-Total", total.ToString());

        var page = await query
            .OrderByDescending(w => w.CreatedAt)
            .Skip(offset)
            .Take(take)
            .ToListAsync(ct);

        var memberCounts = await db.WorkspaceMembers.AsNoTracking()
            .Where(m => m.LeaveAt == null && page.Select(w => w.Id).Contains(m.WorkspaceId))
            .GroupBy(m => m.WorkspaceId)
            .Select(g => new { WorkspaceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.WorkspaceId, x => x.Count, ct);

        return Ok(page.Select(w => new WorkspaceAdminSummary
        {
            Id = w.Id,
            Slug = w.Slug,
            Name = w.Name,
            Type = w.Type,
            Plan = w.Plan,
            OwnerAccountId = w.OwnerAccountId,
            IsBundled = w.IsBundled,
            MemberCount = memberCounts.GetValueOrDefault(w.Id),
            DeletedAt = w.DeletedAt,
            CreatedAt = w.CreatedAt,
            UpdatedAt = w.UpdatedAt
        }).ToList());
    }

    [HttpGet("{id:guid}")]
    [AskPermission(PermissionKeys.AdminWorkspacesView)]
    public async Task<ActionResult<WorkspaceAdminDetailResponse>> GetWorkspace(Guid id, CancellationToken ct)
    {
        var workspace = await db.Workspaces.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id, ct);
        if (workspace is null)
            return NotFound(ApiError.NotFound("workspace", code: "WORKSPACE_NOT_FOUND"));

        var members = await db.WorkspaceMembers.AsNoTracking()
            .Where(m => m.WorkspaceId == id && m.LeaveAt == null)
            .ToListAsync(ct);
        var rolePermissions = await db.WorkspaceRolePermissions.AsNoTracking()
            .Where(p => p.WorkspaceId == id)
            .OrderBy(p => p.RoleLevel)
            .ToListAsync(ct);
        var userPermissions = await db.WorkspaceUserPermissions.AsNoTracking()
            .Where(p => p.WorkspaceId == id)
            .ToListAsync(ct);
        var bundledPlans = await db.WorkspaceBundledPlans.AsNoTracking()
            .Where(b => b.WorkspaceId == id)
            .ToListAsync(ct);

        return Ok(new WorkspaceAdminDetailResponse
        {
            Workspace = workspace,
            Members = members,
            RolePermissions = rolePermissions,
            UserPermissions = userPermissions,
            BundledPlans = bundledPlans
        });
    }

    [HttpPatch("{id:guid}")]
    [AskPermission(PermissionKeys.AdminWorkspacesManage)]
    public async Task<ActionResult<WtWorkspace>> UpdateWorkspace(Guid id, [FromBody] UpdateWorkspaceRequest request, CancellationToken ct)
    {
        var workspace = await db.Workspaces.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (workspace is null)
            return NotFound(ApiError.NotFound("workspace", code: "WORKSPACE_NOT_FOUND"));

        if (!string.IsNullOrWhiteSpace(request.Name)) workspace.Name = request.Name;
        if (request.Description is not null) workspace.Description = request.Description;

        if (request.Slug is { Length: > 0 })
        {
            var slug = request.Slug.Trim();
            if (await db.Workspaces.IgnoreQueryFilters().AnyAsync(w => w.Slug == slug && w.Id != id, ct))
                return Conflict(ApiError.Conflict("Workspace slug is already taken.", code: "WORKSPACE_SLUG_CONFLICT"));
            workspace.Slug = slug;
        }

        db.Workspaces.Update(workspace);
        await db.SaveChangesAsync(ct);
        return Ok(workspace);
    }

    [HttpPut("{id:guid}/plan")]
    [AskPermission(PermissionKeys.AdminWorkspacesPlansManage)]
    public async Task<ActionResult<WtWorkspace>> UpdateWorkspacePlan(Guid id, [FromBody] UpdateWorkspacePlanRequest request, CancellationToken ct)
    {
        var workspace = await db.Workspaces.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (workspace is null)
            return NotFound(ApiError.NotFound("workspace", code: "WORKSPACE_NOT_FOUND"));

        workspace.Plan = request.Plan;
        workspace.PlanExpiresAt = request.PlanExpiresAt;
        if (request.IsBundled.HasValue) workspace.IsBundled = request.IsBundled.Value;

        db.Workspaces.Update(workspace);
        await db.SaveChangesAsync(ct);
        return Ok(workspace);
    }

    [HttpDelete("{id:guid}")]
    [AskPermission(PermissionKeys.AdminWorkspacesDelete)]
    public async Task<IActionResult> DeleteWorkspace(Guid id, CancellationToken ct)
    {
        var workspace = await db.Workspaces.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (workspace is null)
            return NotFound(ApiError.NotFound("workspace", code: "WORKSPACE_NOT_FOUND"));

        workspace.DeletedAt = clock.GetCurrentInstant();
        db.Workspaces.Update(workspace);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
