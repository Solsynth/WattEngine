using System;
using System.Collections.Generic;
using System.Linq;
using DysonNetwork.Shared.Proto;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NodaTime;
using WattEngine.Ideask.Broad;

namespace WattEngine.Ideask.Services;

public class TaskService(AppDatabase db, IHttpContextAccessor httpContextAccessor, ILogger<TaskService> logger)
{
    private Guid GetCurrentAccountId()
    {
        var currentUser = httpContextAccessor.HttpContext?.Items["CurrentUser"] as Account;
        if (currentUser == null) throw new UnauthorizedAccessException("User not authenticated");
        return Guid.Parse(currentUser.Id);
    }

    public async global::System.Threading.Tasks.Task<WattEngine.Ideask.Task.WtTask> CreateTaskAsync(Guid broadId, string name, Guid? parentTaskId, List<Guid>? assigneeAccountIds)
    {
        var accountId = GetCurrentAccountId();
        var broad = await db.Broads.FirstOrDefaultAsync(b => b.Id == broadId);
        if (broad == null) throw new KeyNotFoundException("Broad not found");

        // Check access to broad
        if (broad.AccountId != accountId && (broad.Project == null || (broad.Project.CreatorAccountId != accountId && !broad.Project.Members.Any(m => m.AccountId == accountId))))
            throw new UnauthorizedAccessException("No access to broad");

        if (parentTaskId.HasValue)
        {
            var parent = await db.Tasks.FirstOrDefaultAsync(t => t.Id == parentTaskId.Value && t.BroadId == broadId);
            if (parent == null) throw new KeyNotFoundException("Parent task not found in this broad");
        }

        var task = new WattEngine.Ideask.Task.WtTask
        {
            Name = name,
            BroadId = broadId,
            ParentTaskId = parentTaskId
        };

        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        if (assigneeAccountIds != null && assigneeAccountIds.Any())
        {
            await AssignTaskAsync(task.Id, assigneeAccountIds);
        }

        return task;
    }

    public async global::System.Threading.Tasks.Task<List<WattEngine.Ideask.Task.WtTask>> GetTasksAsync(Guid broadId)
    {
        var accountId = GetCurrentAccountId();
        var broad = await db.Broads.FirstOrDefaultAsync(b => b.Id == broadId);
        if (broad == null) throw new KeyNotFoundException("Broad not found");

        if (broad.AccountId != accountId && (broad.Project == null || (broad.Project.CreatorAccountId != accountId && !broad.Project.Members.Any(m => m.AccountId == accountId))))
            throw new UnauthorizedAccessException("No access to broad");

        return await db.Tasks
            .Where(t => t.BroadId == broadId)
            .ToListAsync();
    }

    public async global::System.Threading.Tasks.Task<WattEngine.Ideask.Task.WtTask?> GetTaskAsync(Guid taskId)
    {
        var accountId = GetCurrentAccountId();
        var task = await db.Tasks
            .Include(t => t.Assignees)
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task == null) return null;

        var broad = task.Broad;
        if (broad.AccountId != accountId && (broad.Project == null || (broad.Project.CreatorAccountId != accountId && !broad.Project.Members.Any(m => m.AccountId == accountId))))
            throw new UnauthorizedAccessException("No access to task");

        return task;
    }

    public async global::System.Threading.Tasks.Task<WattEngine.Ideask.Task.WtTask> UpdateTaskAsync(Guid taskId, string name, WattEngine.Ideask.Task.TaskCompleteReason? completeReason)
    {
        var task = await GetTaskAsync(taskId);
        if (task == null) throw new KeyNotFoundException("Task not found");
        task.Name = name;
        task.CompleteReason = completeReason;
        if (completeReason.HasValue) task.CompletedAt = SystemClock.Instance.GetCurrentInstant();
        await db.SaveChangesAsync();
        return task;
    }

    public async global::System.Threading.Tasks.Task DeleteTaskAsync(Guid taskId)
    {
        var task = await GetTaskAsync(taskId);
        if (task == null) throw new KeyNotFoundException("Task not found");
        db.Tasks.Remove(task);
        await db.SaveChangesAsync();
    }

    public async global::System.Threading.Tasks.Task AssignTaskAsync(Guid taskId, List<Guid> assigneeAccountIds)
    {
        var task = await db.Tasks
            .Include(t => t.Broad)
            .ThenInclude(b => b.Project)
            .ThenInclude(p => p.Members)
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task == null) throw new KeyNotFoundException("Task not found");

        var accountId = GetCurrentAccountId();
        var broad = task.Broad;
        if (broad.AccountId != accountId && (broad.Project == null || (broad.Project.CreatorAccountId != accountId && !broad.Project.Members.Any(m => m.AccountId == accountId))))
            throw new UnauthorizedAccessException("No access to task");

        var validMembers = broad.Project?.Members.Select(m => m.AccountId).ToList() ?? new List<Guid>();
        if (!assigneeAccountIds.All(id => validMembers.Contains(id) || id == broad.AccountId))
            throw new InvalidOperationException("Assignees must be project members or broad creator");

        var assignees = await db.ProjectMembers
            .Where(pm => assigneeAccountIds.Contains(pm.AccountId) && pm.ProjectId == broad.ProjectId)
            .ToListAsync();

        task.Assignees.Clear();
        foreach (var assignee in assignees)
        {
            task.Assignees.Add(assignee);
        }

        await db.SaveChangesAsync();
    }

    public async global::System.Threading.Tasks.Task UnassignTaskAsync(Guid taskId, Guid assigneeAccountId)
    {
        var task = await GetTaskAsync(taskId);
        if (task == null) throw new KeyNotFoundException("Task not found");

        var assignee = task.Assignees.FirstOrDefault(a => a.AccountId == assigneeAccountId);
        if (assignee == null) throw new KeyNotFoundException("Assignee not found");

        task.Assignees.Remove(assignee);
        await db.SaveChangesAsync();
    }
}