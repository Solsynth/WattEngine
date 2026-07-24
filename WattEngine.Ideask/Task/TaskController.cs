using System.ComponentModel.DataAnnotations;
using System.Globalization;
using DysonNetwork.Shared.Capabilities;
using DysonNetwork.Shared.Models;
using DysonNetwork.Shared.Proto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WattEngine.Ideask.Task;

[ApiController]
[ApiFeature("ideask.tasks", Revision = 1)]
[Route("/api")]
public class TaskController(TaskService taskService, DyFileService.DyFileServiceClient files) : ControllerBase
{
    public record CreateTaskRequest(
        [Required]
        [MinLength(1)]
        [MaxLength(1024)]
        string Name,
        [MaxLength(8192)]
        string? Description,
        string? Content,
        List<string>? AttachmentIds,
        [Range(0, int.MaxValue)]
        int Priority,
        NodaTime.Instant? DeadlineAt,
        Guid? ParentTaskId,
        List<Guid>? AssigneeAccountIds,
        Guid? GroupId = null,
        List<string>? Tags = null
    );

    public record UpdateTaskRequest(
        [Required]
        [MinLength(1)]
        [MaxLength(1024)]
        string Name,
        [MaxLength(8192)]
        string? Description,
        string? Content,
        List<string>? AttachmentIds,
        [Range(0, int.MaxValue)]
        int? Priority,
        NodaTime.Instant? DeadlineAt,
        TaskCompleteReason? CompleteReason,
        Guid? GroupId = null,
        bool? Ungroup = null,
        List<string>? Tags = null
    );

    public record CreateTaskGroupRequest(
        [Required, MinLength(1), MaxLength(256)] string Name,
        [Range(0, int.MaxValue)] int? Position = null
    );

    public record UpdateTaskGroupRequest(
        [Required, MinLength(1), MaxLength(256)] string Name,
        [Range(0, int.MaxValue)] int? Position = null
    );

    public record AssignTaskRequest(
        [Required] List<Guid> AssigneeAccountIds
    );

    [HttpGet("broads/{broadId:guid}/tasks")]
    [Authorize]
    public async Task<IActionResult> ListTasks([FromRoute] Guid broadId)
    {
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser)
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
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser)
            return Unauthorized();

        try
        {
            var task = await taskService.GetTaskAsync(taskId);
            if (task == null) return NotFound("Task not found or no access");
            return Ok(task);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet("broads/{broadId:guid}/groups")]
    [Authorize]
    public async Task<IActionResult> ListTaskGroups([FromRoute] Guid broadId)
    {
        try { return Ok(await taskService.GetTaskGroupsAsync(broadId)); }
        catch (KeyNotFoundException) { return NotFound("Broad not found"); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("broads/{broadId:guid}/groups")]
    [Authorize]
    public async Task<IActionResult> CreateTaskGroup([FromRoute] Guid broadId, [FromBody] CreateTaskGroupRequest request)
    {
        try { return Ok(await taskService.CreateTaskGroupAsync(broadId, request.Name, request.Position)); }
        catch (KeyNotFoundException) { return NotFound("Broad not found"); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }

    [HttpPatch("task-groups/{groupId:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateTaskGroup([FromRoute] Guid groupId, [FromBody] UpdateTaskGroupRequest request)
    {
        try { return Ok(await taskService.UpdateTaskGroupAsync(groupId, request.Name, request.Position)); }
        catch (KeyNotFoundException) { return NotFound("Task group not found"); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }

    [HttpDelete("task-groups/{groupId:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteTaskGroup([FromRoute] Guid groupId)
    {
        try { await taskService.DeleteTaskGroupAsync(groupId); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound("Task group not found"); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("broads/{broadId:guid}/tasks")]
    [Authorize]
    public async Task<IActionResult> CreateTask([FromRoute] Guid broadId, [FromBody] CreateTaskRequest request)
    {
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser)
            return Unauthorized();

        try
        {
            // Convert attachment IDs to SnCloudFileReferenceObject
            List<SnCloudFileReferenceObject>? attachments = null;
            if (request.AttachmentIds is not null)
            {
                attachments = new List<SnCloudFileReferenceObject>();
                foreach (var attachmentId in request.AttachmentIds)
                {
                    var file = await files.GetFileAsync(new DyGetFileRequest { Id = attachmentId });
                    if (file is null)
                        return BadRequest($"Attachment file with ID {attachmentId.ToString()} not found");
                    attachments.Add(SnCloudFileReferenceObject.FromProtoValue(file));
                }
            }

            var task = await taskService.CreateTaskAsync(
                broadId,
                request.Name,
                request.Description,
                request.Content,
                attachments,
                request.Priority,
                request.DeadlineAt,
                request.ParentTaskId,
                request.AssigneeAccountIds,
                request.GroupId,
                request.Tags
            );
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
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser)
            return Unauthorized();

        try
        {
            // Convert attachment IDs to SnCloudFileReferenceObject
            if (request.GroupId.HasValue && request.Ungroup == true)
                return BadRequest("GroupId and Ungroup cannot both be specified.");

            List<SnCloudFileReferenceObject>? attachments = null;
            if (request.AttachmentIds is not null)
            {
                attachments = [];
                foreach (var attachmentId in request.AttachmentIds)
                {
                    var file = await files.GetFileAsync(new DyGetFileRequest { Id = attachmentId });
                    if (file is null)
                        return BadRequest($"Attachment file with ID {attachmentId.ToString()} not found");
                    attachments.Add(SnCloudFileReferenceObject.FromProtoValue(file));
                }
            }

            var task = await taskService.UpdateTaskAsync(
                taskId,
                request.Name,
                request.Description,
                request.Content,
                attachments,
                request.Priority,
                request.DeadlineAt,
                request.CompleteReason,
                request.GroupId,
                request.Ungroup == true,
                request.Tags
            );
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
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser)
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
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser)
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
        if (HttpContext.Items["CurrentUser"] is not SnAccount currentUser)
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
