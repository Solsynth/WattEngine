using System.ComponentModel.DataAnnotations;
using DysonNetwork.Shared.Proto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WattEngine.Ideask.Task;

namespace WattEngine.Ideask.Controllers;

[ApiController]
[Route("/api")]
public class TaskController(TaskService taskService) : ControllerBase
{
    public record CreateTaskRequest(
        [Required]
        [MinLength(1)]
        [MaxLength(1024)]
        string Name,
        Guid? ParentTaskId,
        List<Guid>? AssigneeAccountIds
    );

    public record UpdateTaskRequest(
        [Required]
        [MinLength(1)]
        [MaxLength(1024)]
        string Name,
        TaskCompleteReason? CompleteReason
    );

    public record AssignTaskRequest(
        [Required] List<Guid> AssigneeAccountIds
    );

    [HttpGet("broads/{broadId:guid}/tasks")]
    [Authorize]
    public async Task<IActionResult> ListTasks([FromRoute] Guid broadId)
    {
        if (HttpContext.Items["CurrentUser"] is not Account currentUser)
            return Unauthorized();

        try
        {
            var tasks = await taskService.GetTasksAsync(broadId);
            return Ok(tasks);
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Broad not found");
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("tasks/{taskId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetTask([FromRoute] Guid taskId)
    {
        if (HttpContext.Items["CurrentUser"] is not Account currentUser)
            return Unauthorized();

        var task = await taskService.GetTaskAsync(taskId);
        if (task == null) return NotFound("Task not found or no access");
        return Ok(task);
    }

    [HttpPost("broads/{broadId:guid}/tasks")]
    [Authorize]
    public async Task<IActionResult> CreateTask([FromRoute] Guid broadId, [FromBody] CreateTaskRequest request)
    {
        if (HttpContext.Items["CurrentUser"] is not Account currentUser)
            return Unauthorized();

        try
        {
            var task = await taskService.CreateTaskAsync(broadId, request.Name, request.ParentTaskId, request.AssigneeAccountIds);
            return CreatedAtAction(
                nameof(GetTask),
                new { taskId = task.Id },
                task
            );
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPatch("tasks/{taskId:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateTask([FromRoute] Guid taskId, [FromBody] UpdateTaskRequest request)
    {
        if (HttpContext.Items["CurrentUser"] is not Account currentUser)
            return Unauthorized();

        try
        {
            var task = await taskService.UpdateTaskAsync(taskId, request.Name, request.CompleteReason);
            return Ok(task);
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Task not found");
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("tasks/{taskId:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteTask([FromRoute] Guid taskId)
    {
        if (HttpContext.Items["CurrentUser"] is not Account currentUser)
            return Unauthorized();

        try
        {
            await taskService.DeleteTaskAsync(taskId);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Task not found");
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("tasks/{taskId:guid}/assignees")]
    [Authorize]
    public async Task<IActionResult> AssignTask([FromRoute] Guid taskId, [FromBody] AssignTaskRequest request)
    {
        if (HttpContext.Items["CurrentUser"] is not Account currentUser)
            return Unauthorized();

        try
        {
            await taskService.AssignTaskAsync(taskId, request.AssigneeAccountIds);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Task not found");
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
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

    [HttpDelete("tasks/{taskId:guid}/assignees/{assigneeAccountId:guid}")]
    [Authorize]
    public async Task<IActionResult> UnassignTask([FromRoute] Guid taskId, [FromRoute] Guid assigneeAccountId)
    {
        if (HttpContext.Items["CurrentUser"] is not Account currentUser)
            return Unauthorized();

        try
        {
            await taskService.UnassignTaskAsync(taskId, assigneeAccountId);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Task or assignee not found");
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
}