using DysonNetwork.Shared;
using DysonNetwork.Shared.Models;
using DysonNetwork.Shared.Proto;
using DysonNetwork.Shared.Registry;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using WattEngine.Ideask.Broad;
using WattEngine.Ideask.Connectivity;
using WattEngine.Ideask.Models.WebSocket;
using WattEngine.Ideask.GitHub;

namespace WattEngine.Ideask.Task;

#pragma warning disable CS9113
public class TaskService(
    AppDatabase db,
    IHttpContextAccessor httpContextAccessor,
    ILogger<TaskService> logger,
    RealtimeDeliveryService webSocketService,
    RemoteWorkspaceService workspaces,
    GitHubIntegrationService githubIntegration
)
#pragma warning restore CS9113
{
    private Guid GetCurrentAccountId()
    {
        var currentUser = httpContextAccessor.HttpContext?.Items["CurrentUser"] as SnAccount;
        if (currentUser == null) throw new UnauthorizedAccessException("User not authenticated");
        return currentUser.Id;
    }

    private async System.Threading.Tasks.Task<WtBroad> GetOwnedBroad(Guid broadId)
    {
        var broad = await db.Broads.FirstOrDefaultAsync(b => b.Id == broadId)
            ?? throw new KeyNotFoundException("Broad not found");
        if (broad.AccountId != GetCurrentAccountId())
            throw new UnauthorizedAccessException("No access to broad");
        return broad;
    }

    private async System.Threading.Tasks.Task EnsureGroupBelongsToBroad(Guid groupId, Guid broadId)
    {
        if (!await db.TaskGroups.AnyAsync(g => g.Id == groupId && g.BroadId == broadId))
            throw new KeyNotFoundException("Task group not found in this broad");
    }

    private static List<string> NormalizeTags(IEnumerable<string>? tags)
    {
        if (tags is null) return [];
        var normalized = tags.Select(tag => tag.Trim()).ToList();
        if (normalized.Any(string.IsNullOrWhiteSpace) || normalized.Any(tag => tag.Length > 128))
            throw new ArgumentException("Tags must be non-empty and at most 128 characters.");
        return normalized.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async System.Threading.Tasks.Task<WtTask> CreateTaskAsync(
        Guid broadId,
        string name,
        string? description,
        string? content,
        List<SnCloudFileReferenceObject>? attachments,
        int priority,
        NodaTime.Instant? deadlineAt,
        Guid? parentTaskId,
        List<Guid>? assigneeAccountIds,
        Guid? groupId,
        List<string>? tags)
    {
        var accountId = GetCurrentAccountId();
        var broad = await db.Broads.FirstOrDefaultAsync(b => b.Id == broadId);
        if (broad == null) throw new KeyNotFoundException("Broad not found");

        if (broad.AccountId != accountId)
            throw new UnauthorizedAccessException("No access to broad");

        // Check tasks quota if the broad belongs to a workspace
        if (broad.WorkspaceId != null)
        {
            await CheckTaskQuota(broad);
        }

        if (parentTaskId.HasValue)
        {
            var parent = await db.Tasks.FirstOrDefaultAsync(t => t.Id == parentTaskId.Value && t.BroadId == broadId);
            if (parent == null) throw new KeyNotFoundException("Parent task not found in this broad");
        }

        if (groupId.HasValue)
            await EnsureGroupBelongsToBroad(groupId.Value, broadId);

        var task = new WtTask
        {
            Name = name,
            Description = description,
            Content = content,
            Attachments = attachments ?? new List<SnCloudFileReferenceObject>(),
            Priority = priority,
            DeadlineAt = deadlineAt,
            BroadId = broadId,
            ParentTaskId = parentTaskId,
            GroupId = groupId,
            Tags = NormalizeTags(tags)
        };

        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        // Send WebSocket notification
        var packet = webSocketService.CreateTaskCreatedPacket(task, broad, accountId);
        var userIds = new List<string> { accountId.ToString() };
        await webSocketService.SendTaskPacketAsync(broad, userIds, packet);

        if (assigneeAccountIds != null && assigneeAccountIds.Any())
        {
            await AssignTaskAsync(task.Id, assigneeAccountIds);
        }

        await githubIntegration.SyncLocalTaskAsync(task.Id);

        return task;
    }

    private async System.Threading.Tasks.Task CheckTaskQuota(WtBroad broad)
    {
        var plan = (WorkspacePlan)await workspaces.GetPlan(broad.WorkspaceId!.Value);
        var maxTasks = WorkspacePlanQuota.GetMaxTasksPerProject(plan);

        var taskCount = await db.Tasks
            .CountAsync(t => t.BroadId == broad.Id && t.DeletedAt == null);

        if (taskCount >= maxTasks)
            throw new InvalidOperationException(
                $"Workspace plan ({plan}) allows max {maxTasks} tasks per broad. Current count: {taskCount}."
            );
    }

    public async System.Threading.Tasks.Task<List<WtTask>> GetTasksAsync(Guid broadId)
    {
        var accountId = GetCurrentAccountId();
        var broad = await db.Broads.FirstOrDefaultAsync(b => b.Id == broadId);
        if (broad == null) throw new KeyNotFoundException("Broad not found");

        if (broad.AccountId != accountId)
            throw new UnauthorizedAccessException("No access to broad");

        return await db.Tasks
            .Where(t => t.BroadId == broadId)
            .Include(t => t.GitHubIssues)
            .ToListAsync();
    }

    public async System.Threading.Tasks.Task<WtTask?> GetTaskAsync(Guid taskId)
    {
        var accountId = GetCurrentAccountId();
        var task = await db.Tasks
            .Include(t => t.Broad)
            .Include(t => t.Assignees)
            .Include(t => t.GitHubIssues)
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task == null) return null;

        var broad = task.Broad;
        if (broad.AccountId != accountId)
            throw new UnauthorizedAccessException("No access to task");

        return task;
    }

    public async System.Threading.Tasks.Task<WtTask> UpdateTaskAsync(
        Guid taskId,
        string name,
        string? description,
        string? content,
        List<SnCloudFileReferenceObject>? attachments,
        int? priority,
        NodaTime.Instant? deadlineAt,
        TaskCompleteReason? completeReason,
        Guid? groupId,
        bool ungroup,
        List<string>? tags)
    {
        var accountId = GetCurrentAccountId();
        var task = await db.Tasks
            .Include(t => t.Broad)
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task == null) throw new KeyNotFoundException("Task not found");

        var broad = task.Broad;
        if (broad.AccountId != accountId)
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

        if (tags is not null)
        {
            var normalizedTags = NormalizeTags(tags);
            if (!normalizedTags.SequenceEqual(task.Tags, StringComparer.OrdinalIgnoreCase))
            {
                task.Tags = normalizedTags;
                changedProperties.Add("tags");
            }
        }

        if (groupId.HasValue)
        {
            await EnsureGroupBelongsToBroad(groupId.Value, task.BroadId);
            if (task.GroupId != groupId)
            {
                task.GroupId = groupId;
                changedProperties.Add("group_id");
            }
        }
        else if (ungroup && task.GroupId.HasValue)
        {
            task.GroupId = null;
            changedProperties.Add("group_id");
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
            var packet = webSocketService.CreateTaskUpdatedPacket(task, broad, changedProperties, accountId);
            await webSocketService.SendTaskPacketAsync(broad, [accountId.ToString()], packet);
        }

        if (changedProperties.Any(property => property is "name" or "content" or "tags" or "complete_reason"))
            await githubIntegration.SyncLocalTaskAsync(task.Id);

        return task;
    }

    public async System.Threading.Tasks.Task<List<WtTaskGroup>> GetTaskGroupsAsync(Guid broadId)
    {
        await GetOwnedBroad(broadId);
        return await db.TaskGroups.Where(g => g.BroadId == broadId).OrderBy(g => g.Position).ToListAsync();
    }

    public async System.Threading.Tasks.Task<WtTaskGroup> CreateTaskGroupAsync(Guid broadId, string name, int? position)
    {
        await GetOwnedBroad(broadId);
        var nextPosition = (await db.TaskGroups.Where(g => g.BroadId == broadId)
            .MaxAsync(g => (int?)g.Position) ?? -1) + 1;
        var group = new WtTaskGroup { BroadId = broadId, Name = name, Position = position ?? nextPosition };
        db.TaskGroups.Add(group);
        await db.SaveChangesAsync();
        return group;
    }

    public async System.Threading.Tasks.Task<WtTaskGroup> UpdateTaskGroupAsync(Guid groupId, string name, int? position)
    {
        var group = await db.TaskGroups.Include(g => g.Broad).FirstOrDefaultAsync(g => g.Id == groupId)
            ?? throw new KeyNotFoundException("Task group not found");
        if (group.Broad.AccountId != GetCurrentAccountId())
            throw new UnauthorizedAccessException("No access to task group");
        group.Name = name;
        if (position.HasValue) group.Position = position.Value;
        await db.SaveChangesAsync();
        return group;
    }

    public async System.Threading.Tasks.Task DeleteTaskGroupAsync(Guid groupId)
    {
        var group = await db.TaskGroups.Include(g => g.Broad).FirstOrDefaultAsync(g => g.Id == groupId)
            ?? throw new KeyNotFoundException("Task group not found");
        if (group.Broad.AccountId != GetCurrentAccountId())
            throw new UnauthorizedAccessException("No access to task group");

        var groupedTasks = await db.Tasks.Where(task => task.GroupId == groupId).ToListAsync();
        foreach (var task in groupedTasks)
            task.GroupId = null;

        db.TaskGroups.Remove(group);
        await db.SaveChangesAsync();
    }

    public async System.Threading.Tasks.Task DeleteTaskAsync(Guid taskId)
    {
        var accountId = GetCurrentAccountId();
        var task = await db.Tasks
            .Include(t => t.Broad)
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task == null) throw new KeyNotFoundException("Task not found");

        var broad = task.Broad;
        if (broad.AccountId != accountId)
            throw new UnauthorizedAccessException("No access to task");

        db.Tasks.Remove(task);
        await db.SaveChangesAsync();

        var packet = webSocketService.CreateTaskUpdatedPacket(
            task,
            broad,
            new List<string> { "deleted" },
            accountId
        );
        await webSocketService.SendTaskPacketAsync(broad, [accountId.ToString()], packet);
    }

    public async System.Threading.Tasks.Task AssignTaskAsync(Guid taskId, List<Guid> assigneeAccountIds)
    {
        var task = await db.Tasks
            .Include(t => t.Broad)
            .Include(t => t.Assignees)
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task == null) throw new KeyNotFoundException("Task not found");

        var accountId = GetCurrentAccountId();
        var broad = task.Broad;
        if (broad.AccountId != accountId)
            throw new UnauthorizedAccessException("No access to task");

        var existingAssigneeIds = task.Assignees.Select(a => a.AccountId).ToList();
        var newAssigneeIds = assigneeAccountIds.Except(existingAssigneeIds).ToList();
        var removedAccountIds = existingAssigneeIds.Except(assigneeAccountIds).ToList();

        // Remove assignees no longer in the list
        var toRemove = task.Assignees
            .Where(a => removedAccountIds.Contains(a.AccountId))
            .ToList();
        foreach (var assignee in toRemove)
            task.Assignees.Remove(assignee);

        // Add new assignees
        foreach (var newId in newAssigneeIds)
        {
            task.Assignees.Add(new WtTaskAssignee
            {
                TaskId = taskId,
                AccountId = newId
            });
        }

        await db.SaveChangesAsync();

        if (newAssigneeIds.Any() || removedAccountIds.Any())
        {
            var allUserIds = assigneeAccountIds.Select(id => id.ToString()).ToList();
            allUserIds.Add(accountId.ToString());

            var packet = webSocketService.CreateTaskAssignedPacket(
                task,
                broad,
                newAssigneeIds.Select(id => id.ToString()).ToList(),
                removedAccountIds.Select(id => id.ToString()).ToList(),
                accountId
            );
            await webSocketService.SendTaskPacketAsync(broad, allUserIds, packet);
        }
    }

    public async System.Threading.Tasks.Task UnassignTaskAsync(Guid taskId, Guid assigneeAccountId)
    {
        var accountId = GetCurrentAccountId();
        var task = await db.Tasks
            .Include(t => t.Broad)
            .Include(t => t.Assignees)
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task == null) throw new KeyNotFoundException("Task not found");

        var broad = task.Broad;
        if (broad.AccountId != accountId)
            throw new UnauthorizedAccessException("No access to task");

        var assignee = task.Assignees.FirstOrDefault(a => a.AccountId == assigneeAccountId);
        if (assignee == null) throw new KeyNotFoundException("Assignee not found");

        task.Assignees.Remove(assignee);
        await db.SaveChangesAsync();

        var packet = webSocketService.CreateTaskAssignedPacket(
            task,
            broad,
            new List<string>(),
            new List<string> { assigneeAccountId.ToString() },
            accountId
        );
        await webSocketService.SendTaskPacketAsync(
            broad,
            [accountId.ToString(), assigneeAccountId.ToString()],
            packet
        );
    }
}
