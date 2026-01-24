using System.ComponentModel.DataAnnotations;
using DysonNetwork.Shared.Proto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WattEngine.Ideask.Broad;

namespace WattEngine.Ideask.Controllers;

[ApiController]
[Route("/api/projects")]
public class ProjectController(ProjectService projectService) : ControllerBase
{
    public record CreateProjectRequest(
        [Required]
        [MinLength(1)]
        [MaxLength(256)]
        string Name
    );

    public record UpdateProjectRequest(
        [Required]
        [MinLength(1)]
        [MaxLength(256)]
        string Name
    );

    public record AddMemberRequest(
        [Required] Guid AccountId,
        [Required] Permission Permission
    );

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> ListProjects()
    {
        if (HttpContext.Items["CurrentUser"] is not Account currentUser)
            return Unauthorized();

        var accountId = Guid.Parse(currentUser.Id);
        var projects = await projectService.GetProjectsAsync();
        return Ok(projects);
    }

    [HttpGet("{projectId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetProject([FromRoute] Guid projectId)
    {
        if (HttpContext.Items["CurrentUser"] is not Account currentUser)
            return Unauthorized();

        var accountId = Guid.Parse(currentUser.Id);
        var project = await projectService.GetProjectAsync(projectId);
        if (project == null) return NotFound("Project not found or no access");
        return Ok(project);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateProject([FromBody] CreateProjectRequest request)
    {
        if (HttpContext.Items["CurrentUser"] is not Account currentUser)
            return Unauthorized();

        try
        {
            var project = await projectService.CreateProjectAsync(request.Name);
            return CreatedAtAction(
                nameof(GetProject),
                new { projectId = project.Id },
                project
            );
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPatch("{projectId:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateProject([FromRoute] Guid projectId, [FromBody] UpdateProjectRequest request)
    {
        if (HttpContext.Items["CurrentUser"] is not Account currentUser)
            return Unauthorized();

        try
        {
            var project = await projectService.UpdateProjectAsync(projectId, request.Name);
            return Ok(project);
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Project not found");
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{projectId:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteProject([FromRoute] Guid projectId)
    {
        if (HttpContext.Items["CurrentUser"] is not Account currentUser)
            return Unauthorized();

        try
        {
            await projectService.DeleteProjectAsync(projectId);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Project not found");
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{projectId:guid}/members")]
    [Authorize]
    public async Task<IActionResult> AddMember([FromRoute] Guid projectId, [FromBody] AddMemberRequest request)
    {
        if (HttpContext.Items["CurrentUser"] is not Account currentUser)
            return Unauthorized();

        try
        {
            await projectService.AddMemberAsync(projectId, request.AccountId, request.Permission);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Project not found");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{projectId:guid}/members/{memberAccountId:guid}")]
    [Authorize]
    public async Task<IActionResult> RemoveMember([FromRoute] Guid projectId, [FromRoute] Guid memberAccountId)
    {
        if (HttpContext.Items["CurrentUser"] is not Account currentUser)
            return Unauthorized();

        try
        {
            await projectService.RemoveMemberAsync(projectId, memberAccountId);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Project or member not found");
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
}