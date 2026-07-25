using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WattEngine.Ideask.Task;

[ApiController]
[Route("/api")]
public class TaskCommentController(TaskCommentService comments) : ControllerBase
{
    public record CommentRequest([Required, MinLength(1), MaxLength(65536)] string Content);

    [HttpGet("tasks/{taskId:guid}/comments"), Authorize]
    public async Task<IActionResult> List(Guid taskId) => await Execute(() => comments.ListAsync(taskId));

    [HttpPost("tasks/{taskId:guid}/comments"), Authorize]
    public async Task<IActionResult> Create(Guid taskId, [FromBody] CommentRequest request) => await Execute(() => comments.CreateAsync(taskId, request.Content), true);

    [HttpPatch("task-comments/{commentId:guid}"), Authorize]
    public async Task<IActionResult> Update(Guid commentId, [FromBody] CommentRequest request) => await Execute(() => comments.UpdateAsync(commentId, request.Content));

    [HttpDelete("task-comments/{commentId:guid}"), Authorize]
    public async Task<IActionResult> Delete(Guid commentId)
    {
        try { await comments.DeleteAsync(commentId); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    private async Task<IActionResult> Execute<T>(Func<Task<T>> action, bool created = false)
    {
        try { var value = await action(); return created ? StatusCode(StatusCodes.Status201Created, value) : Ok(value); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }
}
