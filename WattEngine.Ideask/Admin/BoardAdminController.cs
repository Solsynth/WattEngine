using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using DysonNetwork.Shared.Auth;
using DysonNetwork.Shared.Capabilities;
using DysonNetwork.Shared.Networking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using WattEngine.Ideask.Broad;
using WattEngine.Ideask.Task;

namespace WattEngine.Ideask.Admin;

/// <summary>
/// Platform-level project-board administration. Every action is gated by an
/// <see cref="AskPermissionAttribute"/> node checked against Padlock by
/// <c>RemotePermissionMiddleware</c>; board ownership is not required.
/// </summary>
[ApiController]
[Route("/api/admin/boards")]
[Authorize]
[ApiFeature("admin.boards", Revision = 1)]
public class BoardAdminController(AppDatabase db) : ControllerBase
{
    private static readonly Regex TaskPrefixPattern = new("^[A-Za-z0-9][A-Za-z0-9_-]{0,31}$", RegexOptions.Compiled);

    public class BoardAdminSummary
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Guid AccountId { get; set; }
        public Guid? WorkspaceId { get; set; }
        public Visibility Visibility { get; set; }
        public string? TaskPrefix { get; set; }
        public int TaskCount { get; set; }
        public Instant? DeletedAt { get; set; }
        public Instant CreatedAt { get; set; }
        public Instant UpdatedAt { get; set; }
    }

    public class BoardAdminTaskSummary
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Guid? GroupId { get; set; }
        public int SerialNumber { get; set; }
        public int Priority { get; set; }
        public TaskCompleteReason? CompleteReason { get; set; }
        public Instant? DeadlineAt { get; set; }
    }

    public class BoardAdminDetailResponse
    {
        public WtBroad Broad { get; set; } = null!;
        public List<BoardAdminTaskSummary> Tasks { get; set; } = [];
    }

    public class UpdateBoardAdminRequest
    {
        [MaxLength(1024)] public string? Name { get; set; }
        [MaxLength(8192)] public string? Description { get; set; }
        public Visibility? Visibility { get; set; }
        [MaxLength(32)] public string? TaskPrefix { get; set; }
        public bool ClearTaskPrefix { get; set; }
    }

    [HttpGet]
    [AskPermission(PermissionKeys.AdminBoardsView)]
    public async Task<ActionResult<List<BoardAdminSummary>>> ListBoards(
        [FromQuery] Guid? workspaceId = null,
        [FromQuery] Guid? accountId = null,
        [FromQuery] Visibility? visibility = null,
        [FromQuery] string? q = null,
        [FromQuery] bool includeDeleted = false,
        [FromQuery] int take = 50,
        [FromQuery] int offset = 0,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);
        offset = Math.Max(offset, 0);

        var query = db.Broads.AsNoTracking();
        if (includeDeleted)
            query = query.IgnoreQueryFilters();
        else
            query = query.Where(b => b.DeletedAt == null);

        if (workspaceId.HasValue) query = query.Where(b => b.WorkspaceId == workspaceId.Value);
        if (accountId.HasValue) query = query.Where(b => b.AccountId == accountId.Value);
        if (visibility.HasValue) query = query.Where(b => b.Visibility == visibility.Value);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var probe = q.Trim();
            query = query.Where(b => EF.Functions.ILike(b.Name, $"%{probe}%"));
        }

        var total = await query.CountAsync(ct);
        Response.Headers.Append("X-Total", total.ToString());

        var page = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip(offset)
            .Take(take)
            .ToListAsync(ct);

        var taskCounts = await db.Tasks.AsNoTracking()
            .Where(t => page.Select(b => b.Id).Contains(t.BroadId))
            .GroupBy(t => t.BroadId)
            .Select(g => new { BroadId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BroadId, x => x.Count, ct);

        return Ok(page.Select(b => new BoardAdminSummary
        {
            Id = b.Id,
            Name = b.Name,
            AccountId = b.AccountId,
            WorkspaceId = b.WorkspaceId,
            Visibility = b.Visibility,
            TaskPrefix = b.TaskPrefix,
            TaskCount = taskCounts.GetValueOrDefault(b.Id),
            DeletedAt = b.DeletedAt,
            CreatedAt = b.CreatedAt,
            UpdatedAt = b.UpdatedAt
        }).ToList());
    }

    [HttpGet("{id:guid}")]
    [AskPermission(PermissionKeys.AdminBoardsView)]
    public async Task<ActionResult<BoardAdminDetailResponse>> GetBoard(Guid id, CancellationToken ct)
    {
        var broad = await db.Broads.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct);
        if (broad is null)
            return NotFound(ApiError.NotFound("board", code: "BOARD_NOT_FOUND"));

        var tasks = await db.Tasks.AsNoTracking()
            .Where(t => t.BroadId == id)
            .OrderBy(t => t.SerialNumber)
            .Select(t => new BoardAdminTaskSummary
            {
                Id = t.Id,
                Name = t.Name,
                GroupId = t.GroupId,
                SerialNumber = t.SerialNumber,
                Priority = t.Priority,
                CompleteReason = t.CompleteReason,
                DeadlineAt = t.DeadlineAt
            })
            .ToListAsync(ct);

        return Ok(new BoardAdminDetailResponse { Broad = broad, Tasks = tasks });
    }

    [HttpPatch("{id:guid}")]
    [AskPermission(PermissionKeys.AdminBoardsManage)]
    public async Task<ActionResult<WtBroad>> UpdateBoard(Guid id, [FromBody] UpdateBoardAdminRequest request, CancellationToken ct)
    {
        var broad = await db.Broads.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (broad is null)
            return NotFound(ApiError.NotFound("board", code: "BOARD_NOT_FOUND"));

        if (!string.IsNullOrWhiteSpace(request.Name)) broad.Name = request.Name;
        if (request.Description is not null) broad.Description = request.Description;
        if (request.Visibility.HasValue) broad.Visibility = request.Visibility.Value;

        if (request.ClearTaskPrefix)
            broad.TaskPrefix = null;
        else if (request.TaskPrefix is { Length: > 0 })
        {
            if (!TaskPrefixPattern.IsMatch(request.TaskPrefix))
                return BadRequest(ApiError.Validation(
                    new Dictionary<string, string[]> { ["task_prefix"] = ["Task prefix must match ^[A-Za-z0-9][A-Za-z0-9_-]{0,31}$."] },
                    code: "BOARD_TASK_PREFIX_INVALID"));
            broad.TaskPrefix = request.TaskPrefix;
        }

        db.Broads.Update(broad);
        await db.SaveChangesAsync(ct);
        return Ok(broad);
    }

    [HttpDelete("{id:guid}")]
    [AskPermission(PermissionKeys.AdminBoardsDelete)]
    public async Task<IActionResult> DeleteBoard(Guid id, CancellationToken ct)
    {
        var broad = await db.Broads.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (broad is null)
            return NotFound(ApiError.NotFound("board", code: "BOARD_NOT_FOUND"));

        db.Broads.Remove(broad);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
