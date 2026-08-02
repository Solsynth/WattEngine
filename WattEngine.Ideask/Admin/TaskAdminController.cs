using System.ComponentModel.DataAnnotations;
using DysonNetwork.Shared.Auth;
using DysonNetwork.Shared.Capabilities;
using DysonNetwork.Shared.Networking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using WattEngine.Ideask.GitHub;
using WattEngine.Ideask.Task;
using TaskStatus = WattEngine.Ideask.Task.TaskStatus;

namespace WattEngine.Ideask.Admin;

/// <summary>
/// Platform-level task administration. Every action is gated by an
/// <see cref="AskPermissionAttribute"/> node checked against Padlock by
/// <c>RemotePermissionMiddleware</c>; board ownership is not required.
/// </summary>
[ApiController]
[Route("/api/admin/tasks")]
[Authorize]
[ApiFeature("admin.tasks", Revision = 1)]
public class TaskAdminController(AppDatabase db, IClock clock) : ControllerBase
{
    public class TaskAdminSummary
    {
        public Guid Id { get; set; }
        public Guid BroadId { get; set; }
        public string Name { get; set; } = string.Empty;
        public Guid? GroupId { get; set; }
        public int SerialNumber { get; set; }
        public int Priority { get; set; }
        public TaskCompleteReason? CompleteReason { get; set; }
        public Instant? DeadlineAt { get; set; }
        public Instant? DeletedAt { get; set; }
        public Instant CreatedAt { get; set; }
        public Instant UpdatedAt { get; set; }
    }

    public class TaskAdminDetailResponse
    {
        public WtTask Task { get; set; } = null!;
        public List<Guid> AssigneeAccountIds { get; set; } = [];
        public List<WtTaskComment> Comments { get; set; } = [];
        public List<WtGitHubIssueLink> GitHubIssues { get; set; } = [];
    }

    public class UpdateTaskAdminRequest
    {
        [MaxLength(4096)] public string? Name { get; set; }
        [MaxLength(8192)] public string? Description { get; set; }
        [Range(0, int.MaxValue)] public int? Priority { get; set; }
        public Instant? DeadlineAt { get; set; }
        /// <summary>true completes the task, false reopens it; null leaves completion untouched.</summary>
        public bool? Complete { get; set; }
    }

    [HttpGet]
    [AskPermission(PermissionKeys.TasksView)]
    public async Task<ActionResult<List<TaskAdminSummary>>> ListTasks(
        [FromQuery] Guid? broadId = null,
        [FromQuery] TaskStatus? status = null,
        [FromQuery] Guid? groupId = null,
        [FromQuery] string? q = null,
        [FromQuery] bool includeDeleted = false,
        [FromQuery] int take = 50,
        [FromQuery] int offset = 0,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);
        offset = Math.Max(offset, 0);

        var query = db.Tasks.AsNoTracking();
        if (includeDeleted)
            query = query.IgnoreQueryFilters();
        else
            query = query.Where(t => t.DeletedAt == null);

        if (broadId.HasValue) query = query.Where(t => t.BroadId == broadId.Value);
        if (groupId.HasValue) query = query.Where(t => t.GroupId == groupId.Value);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var probe = q.Trim();
            query = query.Where(t => EF.Functions.ILike(t.Name, $"%{probe}%"));
        }

        if (status.HasValue)
        {
            query = status.Value switch
            {
                TaskStatus.Open => query.Where(t => t.CompleteReason == null),
                TaskStatus.Completed => query.Where(t => t.CompleteReason == TaskCompleteReason.Completed),
                TaskStatus.Skipped => query.Where(t => t.CompleteReason == TaskCompleteReason.Skipped),
                TaskStatus.Duplicated => query.Where(t => t.CompleteReason == TaskCompleteReason.Duplicated),
                _ => query
            };
        }

        var total = await query.CountAsync(ct);
        Response.Headers.Append("X-Total", total.ToString());

        return Ok(await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip(offset)
            .Take(take)
            .Select(t => new TaskAdminSummary
            {
                Id = t.Id,
                BroadId = t.BroadId,
                Name = t.Name,
                GroupId = t.GroupId,
                SerialNumber = t.SerialNumber,
                Priority = t.Priority,
                CompleteReason = t.CompleteReason,
                DeadlineAt = t.DeadlineAt,
                DeletedAt = t.DeletedAt,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            })
            .ToListAsync(ct));
    }

    [HttpGet("{id:guid}")]
    [AskPermission(PermissionKeys.TasksView)]
    public async Task<ActionResult<TaskAdminDetailResponse>> GetTask(Guid id, CancellationToken ct)
    {
        var task = await db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task is null)
            return NotFound(ApiError.NotFound("task", code: "TASK_NOT_FOUND"));

        var assigneeIds = await db.TaskAssignees.AsNoTracking()
            .Where(a => a.TaskId == id)
            .Select(a => a.AccountId)
            .ToListAsync(ct);
        var comments = await db.TaskComments.AsNoTracking()
            .Where(c => c.TaskId == id)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);
        var githubIssues = await db.GitHubIssueLinks.AsNoTracking()
            .Where(l => l.TaskId == id)
            .ToListAsync(ct);

        return Ok(new TaskAdminDetailResponse
        {
            Task = task,
            AssigneeAccountIds = assigneeIds,
            Comments = comments,
            GitHubIssues = githubIssues
        });
    }

    [HttpPatch("{id:guid}")]
    [AskPermission(PermissionKeys.TasksManage)]
    public async Task<ActionResult<WtTask>> UpdateTask(Guid id, [FromBody] UpdateTaskAdminRequest request, CancellationToken ct)
    {
        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task is null)
            return NotFound(ApiError.NotFound("task", code: "TASK_NOT_FOUND"));

        if (!string.IsNullOrWhiteSpace(request.Name)) task.Name = request.Name;
        if (request.Description is not null) task.Description = request.Description;
        if (request.Priority.HasValue) task.Priority = request.Priority.Value;
        if (request.DeadlineAt.HasValue) task.DeadlineAt = request.DeadlineAt;

        if (request.Complete.HasValue)
        {
            task.CompleteReason = request.Complete.Value ? TaskCompleteReason.Completed : null;
            task.CompletedAt = request.Complete.Value ? clock.GetCurrentInstant() : null;
        }

        db.Tasks.Update(task);
        await db.SaveChangesAsync(ct);
        return Ok(task);
    }

    [HttpDelete("{id:guid}")]
    [AskPermission(PermissionKeys.TasksDelete)]
    public async Task<IActionResult> DeleteTask(Guid id, CancellationToken ct)
    {
        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task is null)
            return NotFound(ApiError.NotFound("task", code: "TASK_NOT_FOUND"));

        db.Tasks.Remove(task);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
