using System.ComponentModel.DataAnnotations;
using DysonNetwork.Shared.Models;
using DysonNetwork.Shared.Proto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WattEngine.Valve.Workspace;

[ApiController]
[Route("/api/workspaces")]
public class WorkspaceController(
    AppDatabase db,
    WorkspaceService ws,
    DyAccountService.DyAccountServiceClient accountGrpc
) : Controller
{
    public class CreateWorkspaceRequest
    {
        [Required, MaxLength(1024)] public string Slug { get; set; } = string.Empty;
        [Required, MaxLength(1024)] public string Name { get; set; } = string.Empty;
        [MaxLength(4096)] public string? Description { get; set; }
        [Required] public WorkspaceType Type { get; set; }
    }

    public class UpdateWorkspaceRequest
    {
        [MaxLength(1024)] public string? Name { get; set; }
        [MaxLength(4096)] public string? Description { get; set; }
    }

    public class InviteMemberRequest
    {
        [Required] public Guid AccountId { get; set; }
        [Required] public int Role { get; set; }
    }

    public class UpdateMemberRoleRequest
    {
        [Required] public int Role { get; set; }
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<List<WtWorkspace>>> ListMyWorkspaces()
    {
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser) return Unauthorized();
        return await ws.GetUserWorkspaces(currentUser.Id);
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult<WtWorkspace>> GetWorkspace(string slug)
    {
        var workspace = await ws.GetBySlug(slug);
        if (workspace is null) return NotFound();
        return workspace;
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<WtWorkspace>> CreateWorkspace([FromBody] CreateWorkspaceRequest request)
    {
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser) return Unauthorized();

        var existing = await ws.GetBySlug(request.Slug);
        if (existing != null)
            return BadRequest("Slug already taken.");

        var workspace = new WtWorkspace
        {
            Slug = request.Slug,
            Name = request.Name,
            Description = request.Description,
            Type = request.Type
        };

        await ws.Create(workspace, currentUser.Id);
        return CreatedAtAction(nameof(GetWorkspace), new { slug = workspace.Slug }, workspace);
    }

    [HttpPatch("{slug}")]
    [Authorize]
    public async Task<ActionResult<WtWorkspace>> UpdateWorkspace(string slug, [FromBody] UpdateWorkspaceRequest request)
    {
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser) return Unauthorized();

        var workspace = await ws.GetBySlug(slug);
        if (workspace is null) return NotFound();

        if (!await ws.IsMemberWithRole(workspace.Id, currentUser.Id, WorkspaceMemberRole.Owner, WorkspaceMemberRole.Admin))
            return StatusCode(403, "Insufficient permissions.");

        if (request.Name != null) workspace.Name = request.Name;
        if (request.Description != null) workspace.Description = request.Description;

        await ws.Update(workspace);
        return workspace;
    }

    [HttpDelete("{slug}")]
    [Authorize]
    public async Task<IActionResult> DeleteWorkspace(string slug)
    {
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser) return Unauthorized();

        var workspace = await ws.GetBySlug(slug);
        if (workspace is null) return NotFound();

        if (workspace.OwnerAccountId != currentUser.Id)
            return StatusCode(403, "Only the owner can delete a workspace.");

        await ws.Delete(workspace);
        return NoContent();
    }

    [HttpGet("{slug}/members")]
    [Authorize]
    public async Task<ActionResult<List<WtWorkspaceMember>>> ListMembers(string slug)
    {
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser) return Unauthorized();

        var workspace = await ws.GetBySlug(slug);
        if (workspace is null) return NotFound();

        if (!await ws.IsMemberWithRole(workspace.Id, currentUser.Id, WorkspaceMemberRole.Viewer))
            return StatusCode(403, "Insufficient permissions.");

        return await ws.GetMembers(workspace.Id);
    }

    [HttpPost("{slug}/members/invite")]
    [Authorize]
    public async Task<ActionResult<WtWorkspaceMember>> InviteMember(string slug, [FromBody] InviteMemberRequest request)
    {
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser) return Unauthorized();

        var workspace = await ws.GetBySlug(slug);
        if (workspace is null) return NotFound();

        if (!await ws.IsMemberWithRole(workspace.Id, currentUser.Id, WorkspaceMemberRole.Admin))
            return StatusCode(403, "Insufficient permissions.");

        if (request.Role > WorkspaceMemberRole.Admin && workspace.OwnerAccountId != currentUser.Id)
            return StatusCode(403, "Only the owner can invite admins.");

        try
        {
            var member = await ws.InviteMember(workspace.Id, request.AccountId, request.Role);
            return member;
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPatch("{slug}/members/{accountId:guid}")]
    [Authorize]
    public async Task<ActionResult<WtWorkspaceMember>> UpdateMemberRole(string slug, Guid accountId, [FromBody] UpdateMemberRoleRequest request)
    {
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser) return Unauthorized();

        var workspace = await ws.GetBySlug(slug);
        if (workspace is null) return NotFound();

        if (!await ws.IsMemberWithRole(workspace.Id, currentUser.Id, WorkspaceMemberRole.Admin))
            return StatusCode(403, "Insufficient permissions.");

        if (request.Role >= WorkspaceMemberRole.Admin && workspace.OwnerAccountId != currentUser.Id)
            return StatusCode(403, "Only the owner can assign admin role.");

        try
        {
            return await ws.UpdateMemberRole(workspace.Id, accountId, request.Role);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{slug}/members/{accountId:guid}")]
    [Authorize]
    public async Task<IActionResult> RemoveMember(string slug, Guid accountId)
    {
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser) return Unauthorized();

        var workspace = await ws.GetBySlug(slug);
        if (workspace is null) return NotFound();

        if (!await ws.IsMemberWithRole(workspace.Id, currentUser.Id, WorkspaceMemberRole.Admin))
            return StatusCode(403, "Insufficient permissions.");

        if (accountId == workspace.OwnerAccountId)
            return BadRequest("Cannot remove the owner.");

        try
        {
            await ws.RemoveMember(workspace.Id, accountId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
