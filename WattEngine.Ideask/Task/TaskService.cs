using DysonNetwork.Shared;
using DysonNetwork.Shared.Models;
using DysonNetwork.Shared.Proto;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using WattEngine.Ideask.Broad;
using WattEngine.Ideask.Connectivity;
using WattEngine.Ideask.Models.WebSocket;

namespace WattEngine.Ideask.Task;

public class TaskService(AppDatabase db, IHttpContextAccessor httpContextAccessor, ILogger<TaskService> logger, RealtimeDeliveryService webSocketService)
{
    private Guid GetCurrentAccountId()
    {
        var currentUser = httpContextAccessor.HttpContext?.Items["CurrentUser"] as Account;
        if (currentUser == null) throw new UnauthorizedAccessException("User not authenticated");
        return Guid.Parse(currentUser.Id);
    }

    public async global::System.Threading.Tasks.Task<WattEngine.Ideask.Task.WtTask> CreateTaskAsync(
        Guid broadId,
        string name,
        string? description,
        string? content,
        List<SnCloudFileReferenceObject>? attachments,
        int priority,
        NodaTime.Instant? deadlineAt,
        Guid? parentTaskId,
        List<Guid>? assigneeAccountIds)
    {
        var accountId = GetCurrentAccountId();
        var broad = await db.Broads
            .Include(b => b.Project)
            .ThenInclude(p => p.Members)
            .FirstOrDefaultAsync(b => b.Id == broadId);
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
            Description = description,
            Content = content,
            Attachments = attachments ?? new List<SnCloudFileReferenceObject>(),
            Priority = priority,
            DeadlineAt = deadlineAt,
            BroadId = broadId,
            ParentTaskId = parentTaskId
        };

        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        // Send WebSocket notification
        var packet = webSocketService.CreateTaskCreatedPacket(task, broad, broad.Project, accountId);
        await SendWebSocketPacketToProjectMembersAsync(broad, packet);

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

    public async global::System.Threading.Tasks.Task<WattEngine.Ideask.Task.WtTask> UpdateTaskAsync(
        Guid taskId,
        string name,
        string? description,
        string? content,
        List<SnCloudFileReferenceObject>? attachments,
        int? priority,
        NodaTime.Instant? deadlineAt,
        WattEngine.Ideask.Task.TaskCompleteReason? completeReason)
    {
        var accountId = GetCurrentAccountId();
        var task = await db.Tasks
            .Include(t => t.Broad)
            .ThenInclude(b => b.Project)
            .FirstOrDefaultAsync(t => t.Id == taskId);
        
        if (task == null) throw new KeyNotFoundException("Task not found");

        // Check access
        var broad = task.Broad;
        if (broad.AccountId != accountId && (broad.Project == null || (broad.Project.CreatorAccountId != accountId && !broad.Project.Members.Any(m => m.AccountId == accountId))))
            throw new UnauthorizedAccessException("No access to task");

        var changedProperties = new List<string>();
        if (task.Name != name)
        {
            task.Name = name;
            changedProperties.Add("name");
        }

        if (task.Description != description)
        {
            task.Description = description;
            changedProperties.Add("description");
        }

        if (task.Content != content)
        {
            task.Content = content;
            changedProperties.Add("content");
        }

        if (attachments != null && !attachments.SequenceEqual(task.Attachments))
        {
            task.Attachments = attachments;
            changedProperties.Add("attachments");
        }

        if (priority.HasValue && task.Priority != priority.Value)
        {
            task.Priority = priority.Value;
            changedProperties.Add("priority");
        }

        if (task.DeadlineAt != deadlineAt)
        {
            task.DeadlineAt = deadlineAt;
            changedProperties.Add("deadline_at");
        }

        if (task.CompleteReason != completeReason)
        {
            task.CompleteReason = completeReason;
            changedProperties.Add("complete_reason");
            if (completeReason.HasValue)
            {
                task.CompletedAt = SystemClock.Instance.GetCurrentInstant();
                changedProperties.Add("completed_at");
            }
            else
            {
                task.CompletedAt = null;
                changedProperties.Add("completed_at");
            }
        }

        await db.SaveChangesAsync();

        if (changedProperties.Any())
        {
            var packet = webSocketService.CreateTaskUpdatedPacket(task, broad, broad.Project, changedProperties, accountId);
            await SendWebSocketPacketToProjectMembersAsync(broad, packet);
        }

        return task;
    }

    public async global::System.Threading.Tasks.Task DeleteTaskAsync(Guid taskId)
    {
        var accountId = GetCurrentAccountId();
        var task = await db.Tasks
            .Include(t => t.Broad)
            .ThenInclude(b => b.Project)
            .FirstOrDefaultAsync(t => t.Id == taskId);
        
        if (task == null) throw new KeyNotFoundException("Task not found");

        // Check access
        var broad = task.Broad;
        if (broad.AccountId != accountId && (broad.Project == null || (broad.Project.CreatorAccountId != accountId && !broad.Project.Members.Any(m => m.AccountId == accountId))))
            throw new UnauthorizedAccessException("No access to task");

        db.Tasks.Remove(task);
        await db.SaveChangesAsync();

        var packet = webSocketService.CreateTaskUpdatedPacket(
            task, 
            broad, 
            broad.Project, 
            new List<string> { "deleted" }, 
            accountId
        );
        await SendWebSocketPacketToProjectMembersAsync(broad, packet);
    }

    public async global::System.Threading.Tasks.Task AssignTaskAsync(Guid taskId, List<Guid> assigneeAccountIds)
    {
        var task = await db.Tasks
            .Include(t => t.Broad)
            .ThenInclude(b => b.Project)
            .ThenInclude(p => p.Members)
            .Include(t => t.Assignees)
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task == null) throw new KeyNotFoundException("Task not found");

        var accountId = GetCurrentAccountId();
        var broad = task.Broad;
        if (broad.AccountId != accountId && (broad.Project == null || (broad.Project.CreatorAccountId != accountId && !broad.Project.Members.Any(m => m.AccountId == accountId))))
            throw new UnauthorizedAccessException("No access to task");

        var validMembers = broad.Project?.Members.Select(m => m.AccountId).ToList() ?? new List<Guid>();
        if (!assigneeAccountIds.All(id => validMembers.Contains(id) || id == broad.AccountId))
            throw new InvalidOperationException("Assignees must be project members or broad creator");

        var existingAssigneeIds = task.Assignees.Select(a => a.AccountId).ToList();
        var newAssigneeIds = assigneeAccountIds.Except(existingAssigneeIds).ToList();
        var removedAssigneeIds = existingAssigneeIds.Except(assigneeAccountIds).ToList();

        var assignees = await db.ProjectMembers
            .Where(pm => assigneeAccountIds.Contains(pm.AccountId) && pm.ProjectId == broad.ProjectId)
            .ToListAsync();

        task.Assignees.Clear();
        foreach (var assignee in assignees)
        {
            task.Assignees.Add(assignee);
        }

        await db.SaveChangesAsync();

        if (newAssigneeIds.Any() || removedAssigneeIds.Any())
        {
            var packet = webSocketService.CreateTaskAssignedPacket(
                task, 
                broad, 
                broad.Project, 
                newAssigneeIds.Select(id => id.ToString()).ToList(),
                removedAssigneeIds.Select(id => id.ToString()).ToList(),
                accountId
            );
            await SendWebSocketPacketToProjectMembersAsync(broad, packet);
        }
    }

    public async global::System.Threading.Tasks.Task UnassignTaskAsync(Guid taskId, Guid assigneeAccountId)
    {
        var accountId = GetCurrentAccountId();
        var task = await db.Tasks
            .Include(t => t.Broad)
            .ThenInclude(b => b.Project)
            .Include(t => t.Assignees)
            .FirstOrDefaultAsync(t => t.Id == taskId);
        
        if (task == null) throw new KeyNotFoundException("Task not found");

        // Check access
        var broad = task.Broad;
        if (broad.AccountId != accountId && (broad.Project == null || (broad.Project.CreatorAccountId != accountId && !broad.Project.Members.Any(m => m.AccountId == accountId))))
            throw new UnauthorizedAccessException("No access to task");

        var assignee = task.Assignees.FirstOrDefault(a => a.AccountId == assigneeAccountId);
        if (assignee == null) throw new KeyNotFoundException("Assignee not found");

        task.Assignees.Remove(assignee);
        await db.SaveChangesAsync();

        var packet = webSocketService.CreateTaskAssignedPacket(
            task, 
            broad, 
            broad.Project, 
            new List<string>(),
            new List<string> { assigneeAccountId.ToString() },
            accountId
        );
        await SendWebSocketPacketToProjectMembersAsync(broad, packet);
    }

    private async global::System.Threading.Tasks.Task SendWebSocketPacketToProjectMembersAsync(WtBroad broad, IdeaskWebSocketPacket packet)
    {
        var userIds = new List<string>();
        
        // Add broad creator
        userIds.Add(broad.AccountId.ToString());
        
        // Add project members if broad belongs to a project
        if (broad.Project != null)
        {
            var projectMembers = await db.ProjectMembers
                .Where(pm => pm.ProjectId == broad.Project.Id)
                .Select(pm => pm.AccountId.ToString())
                .ToListAsync();
            userIds.AddRange(projectMembers);
        }

        await webSocketService.SendToUsersAsync(userIds.Distinct().ToList(), packet);
    }
}