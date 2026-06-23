using DysonNetwork.Shared.Models;
using DysonNetwork.Shared.Proto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WattEngine.Valve.Workspace;

[ApiController]
[Route("/api/workspaces/{slug}/permissions")]
public class PermissionController(
    AppDatabase db,
    WorkspaceService ws,
    PermissionService perms
) : Controller
{
    [HttpGet("check")]
    [Authorize]
    public async Task<ActionResult<object>> CheckPermission(string slug, [FromQuery] string key)
    {
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser) return Unauthorized();

        var workspace = await ws.GetBySlug(slug);
        if (workspace is null) return NotFound();

        var hasPermission = await perms.HasPermission(workspace.Id, currentUser.Id, key);
        return Ok(new { has_permission = hasPermission, key });
    }

    [HttpGet("roles")]
    [Authorize]
    public async Task<ActionResult<List<WtWorkspaceRolePermission>>> GetRolePermissions(string slug)
    {
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser) return Unauthorized();

        var workspace = await ws.GetBySlug(slug);
        if (workspace is null) return NotFound();

        if (!await ws.IsMemberWithRole(workspace.Id, currentUser.Id, WorkspaceMemberRole.Viewer))
            return StatusCode(403, "Insufficient permissions.");

        return await perms.GetAllRolePermissions(workspace.Id);
    }

    [HttpGet("roles/{roleLevel:int}")]
    [Authorize]
    public async Task<ActionResult<WtWorkspaceRolePermission>> GetRolePermission(string slug, int roleLevel)
    {
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser) return Unauthorized();

        var workspace = await ws.GetBySlug(slug);
        if (workspace is null) return NotFound();

        if (!await ws.IsMemberWithRole(workspace.Id, currentUser.Id, WorkspaceMemberRole.Viewer))
            return StatusCode(403, "Insufficient permissions.");

        var permission = await perms.GetRolePermissions(workspace.Id, roleLevel);
        if (permission is null) return NotFound();
        return permission;
    }

    [HttpPut("roles/{roleLevel:int}")]
    [Authorize]
    public async Task<ActionResult<WtWorkspaceRolePermission>> UpdateRolePermission(
        string slug, int roleLevel, [FromBody] WtWorkspaceRolePermission permission)
    {
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser) return Unauthorized();

        var workspace = await ws.GetBySlug(slug);
        if (workspace is null) return NotFound();

        if (!await ws.IsMemberWithRole(workspace.Id, currentUser.Id, WorkspaceMemberRole.Owner))
            return StatusCode(403, "Only the owner can modify role permissions.");

        var result = await perms.UpdateRolePermission(workspace.Id, roleLevel, permission);
        return result;
    }

    [HttpGet("users/{accountId:guid}")]
    [Authorize]
    public async Task<ActionResult<WtWorkspaceUserPermission?>> GetUserPermission(string slug, Guid accountId)
    {
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser) return Unauthorized();

        var workspace = await ws.GetBySlug(slug);
        if (workspace is null) return NotFound();

        if (!await ws.IsMemberWithRole(workspace.Id, currentUser.Id, WorkspaceMemberRole.Admin))
            return StatusCode(403, "Insufficient permissions.");

        return await perms.GetUserPermission(workspace.Id, accountId);
    }

    [HttpPut("users/{accountId:guid}")]
    [Authorize]
    public async Task<ActionResult<WtWorkspaceUserPermission>> UpdateUserPermission(
        string slug, Guid accountId, [FromBody] WtWorkspaceUserPermission permission)
    {
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser) return Unauthorized();

        var workspace = await ws.GetBySlug(slug);
        if (workspace is null) return NotFound();

        if (!await ws.IsMemberWithRole(workspace.Id, currentUser.Id, WorkspaceMemberRole.Admin))
            return StatusCode(403, "Insufficient permissions.");

        var result = await perms.UpdateUserPermission(workspace.Id, accountId, permission);
        return result;
    }
}
