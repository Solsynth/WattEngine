using System.ComponentModel.DataAnnotations;
using DysonNetwork.Shared.Capabilities;
using DysonNetwork.Shared.Auth;
using DysonNetwork.Shared.Models;
using DysonNetwork.Shared.Proto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace WattEngine.Valve.Workspace;

[ApiController]
[ApiFeature("workspaces", Revision = 1)]
[Route("/api/workspaces")]
#pragma warning disable CS9113
public class WorkspaceController(
    AppDatabase db,
    WorkspaceService ws,
    DyFileService.DyFileServiceClient files
) : Controller
#pragma warning restore CS9113
{
    private Task<WtWorkspace?> ResolveWorkspace(string slug)
    {
        return Guid.TryParse(slug, out var id)
            ? ws.GetById(id)
            : ws.GetBySlug(slug);
    }

    public class CreateWorkspaceRequest
    {
        [Required, MaxLength(1024)] public string Slug { get; set; } = string.Empty;
        [Required, MaxLength(1024)] public string Name { get; set; } = string.Empty;
        [MaxLength(4096)] public string? Description { get; set; }
        [Required] public WorkspaceType Type { get; set; }
        public string? PictureId { get; set; }
        public string? BackgroundId { get; set; }
    }

    public class UpdateWorkspaceRequest
    {
        [MaxLength(1024)] public string? Name { get; set; }
        [MaxLength(4096)] public string? Description { get; set; }
        public string? PictureId { get; set; }
        public string? BackgroundId { get; set; }
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

    public class SubscribePlanRequest
    {
        [Required] public WorkspacePlan Plan { get; set; }
    }

    public class ReassignBundledPlanRequest
    {
        [Required] public Guid WorkspaceId { get; set; }
    }

    [HttpGet]
    [Authorize]
    [AskPermission(PermissionKeys.WorkspacesView)]
    public async Task<ActionResult<List<WtWorkspace>>> ListMyWorkspaces()
    {
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser) return Unauthorized();

        // The individual workspace is created lazily on first access: if the user lists
        // workspaces before any backfill ran, create it for them on the spot.
        if (await ws.GetIndividualWorkspace(currentUser.Id) is null)
            await ws.BackfillIndividualWorkspace(currentUser.Id);

        return await ws.GetUserWorkspaces(currentUser.Id);
    }

    [HttpGet("individual")]
    [Authorize]
    [AskPermission(PermissionKeys.WorkspacesView)]
    public async Task<ActionResult<WtWorkspace>> GetMyIndividualWorkspace()
    {
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser) return Unauthorized();

        var workspace = await ws.GetIndividualWorkspace(currentUser.Id);
        return workspace is null ? NotFound() : Ok(workspace);
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult<WtWorkspace>> GetWorkspace(string slug)
    {
        var workspace = await ResolveWorkspace(slug);
        if (workspace is null) return NotFound();
        return workspace;
    }

    [HttpPost]
    [Authorize]
    [AskPermission(PermissionKeys.WorkspacesCreate)]
    public async Task<ActionResult<WtWorkspace>> CreateWorkspace([FromBody] CreateWorkspaceRequest request)
    {
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser) return Unauthorized();

        if (request.Type == WorkspaceType.Individual)
            return BadRequest("Individual workspaces are created automatically with the account.");

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

        if (request.PictureId is not null)
        {
            var picture = await files.GetFileAsync(new DyGetFileRequest { Id = request.PictureId });
            if (picture is null) return BadRequest("Invalid picture ID.");
            workspace.Picture = SnCloudFileReferenceObject.FromProtoValue(picture);
        }

        if (request.BackgroundId is not null)
        {
            var background = await files.GetFileAsync(new DyGetFileRequest { Id = request.BackgroundId });
            if (background is null) return BadRequest("Invalid background ID.");
            workspace.Background = SnCloudFileReferenceObject.FromProtoValue(background);
        }

        try
        {
            await ws.Create(workspace, currentUser.Id);
            return CreatedAtAction(nameof(GetWorkspace), new { slug = workspace.Slug }, workspace);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPatch("{slug}")]
    [Authorize]
    [AskPermission(PermissionKeys.WorkspacesUpdate)]
    public async Task<ActionResult<WtWorkspace>> UpdateWorkspace(string slug, [FromBody] UpdateWorkspaceRequest request)
    {
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser) return Unauthorized();

        var workspace = await ResolveWorkspace(slug);
        if (workspace is null) return NotFound();

        if (!await ws.IsMemberWithRole(workspace.Id, currentUser.Id, WorkspaceMemberRole.Owner, WorkspaceMemberRole.Admin))
            return StatusCode(403, "Insufficient permissions.");

        if (request.Name != null) workspace.Name = request.Name;
        if (request.Description != null) workspace.Description = request.Description;

        if (request.PictureId is not null)
        {
            var picture = await files.GetFileAsync(new DyGetFileRequest { Id = request.PictureId });
            if (picture is null) return BadRequest("Invalid picture ID.");
            workspace.Picture = SnCloudFileReferenceObject.FromProtoValue(picture);
        }

        if (request.BackgroundId is not null)
        {
            var background = await files.GetFileAsync(new DyGetFileRequest { Id = request.BackgroundId });
            if (background is null) return BadRequest("Invalid background ID.");
            workspace.Background = SnCloudFileReferenceObject.FromProtoValue(background);
        }

        await ws.Update(workspace);
        return workspace;
    }

    [HttpDelete("{slug}")]
    [Authorize]
    [AskPermission(PermissionKeys.WorkspacesDelete)]
    public async Task<IActionResult> DeleteWorkspace(string slug)
    {
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser) return Unauthorized();

        var workspace = await ResolveWorkspace(slug);
        if (workspace is null) return NotFound();

        if (workspace.OwnerAccountId != currentUser.Id)
            return StatusCode(403, "Only the owner can delete a workspace.");

        await ws.Delete(workspace);
        return NoContent();
    }

    [HttpGet("{slug}/members")]
    [Authorize]
    [AskPermission(PermissionKeys.WorkspacesView)]
    public async Task<ActionResult<List<WtWorkspaceMember>>> ListMembers(string slug)
    {
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser) return Unauthorized();

        var workspace = await ResolveWorkspace(slug);
        if (workspace is null) return NotFound();

        if (!await ws.IsMemberWithRole(workspace.Id, currentUser.Id, WorkspaceMemberRole.Viewer))
            return StatusCode(403, "Insufficient permissions.");

        var members = await ws.GetMembers(workspace.Id);
        return await ws.LoadMemberAccounts(members);
    }

    [HttpPost("{slug}/members/invite")]
    [Authorize]
    [AskPermission(PermissionKeys.WorkspacesMembersManage)]
    public async Task<ActionResult<WtWorkspaceMember>> InviteMember(string slug, [FromBody] InviteMemberRequest request)
    {
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser) return Unauthorized();

        var workspace = await ResolveWorkspace(slug);
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
    [AskPermission(PermissionKeys.WorkspacesMembersManage)]
    public async Task<ActionResult<WtWorkspaceMember>> UpdateMemberRole(string slug, Guid accountId, [FromBody] UpdateMemberRoleRequest request)
    {
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser) return Unauthorized();

        var workspace = await ResolveWorkspace(slug);
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
    [AskPermission(PermissionKeys.WorkspacesMembersManage)]
    public async Task<IActionResult> RemoveMember(string slug, Guid accountId)
    {
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser) return Unauthorized();

        var workspace = await ResolveWorkspace(slug);
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

    // Plan management endpoints

    [HttpGet("{slug}/plan/status")]
    [Authorize]
    [AskPermission(PermissionKeys.WorkspacesView)]
    public async Task<ActionResult<object>> GetPlanStatus(string slug)
    {
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser) return Unauthorized();

        var workspace = await ResolveWorkspace(slug);
        if (workspace is null) return NotFound();

        if (!await ws.IsMemberWithRole(workspace.Id, currentUser.Id, WorkspaceMemberRole.Viewer))
            return StatusCode(403, "Insufficient permissions.");

        var bundledPlan = await ws.GetBundledPlan(currentUser.Id);

        return Ok(new
        {
            workspace.Plan,
            workspace.PlanExpiresAt,
            workspace.IsBundled,
            bundled_plan = bundledPlan != null ? new
            {
                bundledPlan.IsEnabled,
                workspace_id = bundledPlan.WorkspaceId,
                bundledPlan.LastReassignedAt,
                cooldown_active = bundledPlan.LastReassignedAt.HasValue &&
                    SystemClock.Instance.GetCurrentInstant() < bundledPlan.LastReassignedAt.Value + WorkspacePlanPricing.ReassignCooldown
            } : null,
            prices = new
            {
                pro = WorkspacePlanPricing.GetMonthlyPrice(WorkspacePlan.Pro),
                enterprise = WorkspacePlanPricing.GetMonthlyPrice(WorkspacePlan.Enterprise),
                currency = "golds"
            }
        });
    }

    [HttpPost("{slug}/plan/assign-bundled")]
    [Authorize]
    [AskPermission(PermissionKeys.WorkspacesPlansManage)]
    public async Task<ActionResult<object>> AssignBundledPlan(string slug)
    {
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser) return Unauthorized();

        var workspace = await ResolveWorkspace(slug);
        if (workspace is null) return NotFound();

        if (workspace.OwnerAccountId != currentUser.Id)
            return StatusCode(403, "Only the workspace owner can assign a bundled plan.");

        if (currentUser.PerkLevel < WorkspacePlanPricing.BundledPlanRequiredPerkLevel)
            return BadRequest($"Perk level {WorkspacePlanPricing.BundledPlanRequiredPerkLevel}+ required for bundled Pro plan.");

        try
        {
            var result = await ws.AssignBundledPlan(currentUser.Id, workspace.Id);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{slug}/plan/unassign-bundled")]
    [Authorize]
    [AskPermission(PermissionKeys.WorkspacesPlansManage)]
    public async Task<IActionResult> UnassignBundledPlan(string slug)
    {
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser) return Unauthorized();

        var workspace = await ResolveWorkspace(slug);
        if (workspace is null) return NotFound();

        try
        {
            await ws.UnassignBundledPlan(currentUser.Id);
            return Ok(new { message = "Bundled plan unassigned." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("plan/reassign-bundled")]
    [Authorize]
    [AskPermission(PermissionKeys.WorkspacesPlansManage)]
    public async Task<ActionResult<object>> ReassignBundledPlan([FromBody] ReassignBundledPlanRequest request)
    {
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser) return Unauthorized();

        try
        {
            var result = await ws.AssignBundledPlan(currentUser.Id, request.WorkspaceId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{slug}/plan/subscribe")]
    [Authorize]
    [AskPermission(PermissionKeys.WorkspacesPlansManage)]
    public async Task<ActionResult<object>> SubscribePlan(string slug, [FromBody] SubscribePlanRequest request)
    {
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser) return Unauthorized();

        var workspace = await ResolveWorkspace(slug);
        if (workspace is null) return NotFound();

        if (workspace.OwnerAccountId != currentUser.Id)
            return StatusCode(403, "Only the owner can manage subscriptions.");

        if (request.Plan == WorkspacePlan.Free)
            return BadRequest("Cannot subscribe to Free plan. Use unassign-bundled instead.");

        try
        {
            var order = await ws.CreatePlanOrder(workspace.Id, currentUser.Id, request.Plan);
            return Ok(new
            {
                order_id = order.Id,
                amount = order.Amount,
                currency = order.Currency,
                plan = request.Plan
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
