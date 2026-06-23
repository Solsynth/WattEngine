using DysonNetwork.Shared.Models;
using DysonNetwork.Shared.Proto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WattEngine.Valve.Workspace;

[ApiController]
[Route("/api/workspaces/{slug}/quota")]
public class QuotaController(
    WorkspaceService ws
) : Controller
{
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<object>> GetQuota(string slug)
    {
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser) return Unauthorized();

        var workspace = await ws.GetBySlug(slug);
        if (workspace is null) return NotFound();

        if (!await ws.IsMemberWithRole(workspace.Id, currentUser.Id, WorkspaceMemberRole.Viewer))
            return StatusCode(403, "Insufficient permissions.");

        var plan = workspace.Plan;
        return Ok(new
        {
            plan,
            quotas = new
            {
                max_projects = WorkspacePlanQuota.GetMaxProjects(plan),
                max_members = WorkspacePlanQuota.GetMaxMembers(plan),
                max_tasks_per_project = WorkspacePlanQuota.GetMaxTasksPerProject(plan),
                max_broads_per_project = WorkspacePlanQuota.GetMaxBroadsPerProject(plan),
                max_storage_bytes = WorkspacePlanQuota.GetMaxStorageBytes(plan)
            }
        });
    }

    [HttpGet("check")]
    [Authorize]
    public async Task<ActionResult<object>> CheckQuota(string slug, [FromQuery] string resource)
    {
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser) return Unauthorized();

        var workspace = await ws.GetBySlug(slug);
        if (workspace is null) return NotFound();

        if (!await ws.IsMemberWithRole(workspace.Id, currentUser.Id, WorkspaceMemberRole.Viewer))
            return StatusCode(403, "Insufficient permissions.");

        var plan = workspace.Plan;
        var limit = resource.ToLowerInvariant() switch
        {
            "projects" => WorkspacePlanQuota.GetMaxProjects(plan),
            "members" => WorkspacePlanQuota.GetMaxMembers(plan),
            "tasks" => WorkspacePlanQuota.GetMaxTasksPerProject(plan),
            "broads" => WorkspacePlanQuota.GetMaxBroadsPerProject(plan),
            "storage" => WorkspacePlanQuota.GetMaxStorageBytes(plan),
            _ => (long?)null
        };

        if (limit == null)
            return BadRequest($"Unknown resource type: {resource}");

        return Ok(new
        {
            resource,
            limit,
            plan
        });
    }
}
