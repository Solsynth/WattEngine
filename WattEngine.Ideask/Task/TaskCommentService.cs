using DysonNetwork.Shared.Models;
using Microsoft.EntityFrameworkCore;
using WattEngine.Ideask.Connectivity;
using WattEngine.Ideask.GitHub;
using Task = System.Threading.Tasks.Task;

namespace WattEngine.Ideask.Task;

public class TaskCommentService(AppDatabase db, IHttpContextAccessor context, RealtimeDeliveryService realtime, GitHubIntegrationService github)
{
    private Guid AccountId() => (context.HttpContext?.Items["CurrentUser"] as SnAccount)?.Id ?? throw new UnauthorizedAccessException();

    private async Task<WtTask> OwnedTaskAsync(Guid taskId)
    {
        var task = await db.Tasks.Include(t => t.Broad).SingleOrDefaultAsync(t => t.Id == taskId) ?? throw new KeyNotFoundException("Task not found");
        if (task.Broad.AccountId != AccountId()) throw new UnauthorizedAccessException();
        return task;
    }

    public async Task<List<WtTaskComment>> ListAsync(Guid taskId)
    {
        await OwnedTaskAsync(taskId);
        return await db.TaskComments.Where(c => c.TaskId == taskId).Include(c => c.GitHubComment).OrderBy(c => c.CreatedAt).ToListAsync();
    }

    public async Task<WtTaskComment> CreateAsync(Guid taskId, string content)
    {
        var task = await OwnedTaskAsync(taskId);
        var comment = new WtTaskComment { TaskId = taskId, AuthorAccountId = AccountId(), Content = content };
        db.TaskComments.Add(comment); await db.SaveChangesAsync();
        await github.SyncLocalCommentAsync(comment.Id);
        await realtime.SendTaskPacketAsync(task.Broad, [task.Broad.AccountId.ToString()], realtime.CreateTaskUpdatedPacket(task, task.Broad, ["comments"], AccountId()));
        return comment;
    }

    public async Task<WtTaskComment> UpdateAsync(Guid commentId, string content)
    {
        var comment = await db.TaskComments.Include(c => c.Task).ThenInclude(t => t.Broad).SingleOrDefaultAsync(c => c.Id == commentId) ?? throw new KeyNotFoundException("Comment not found");
        if (comment.Task.Broad.AccountId != AccountId() || comment.AuthorAccountId != AccountId()) throw new UnauthorizedAccessException();
        comment.Content = content; await db.SaveChangesAsync(); await github.SyncLocalCommentAsync(comment.Id); return comment;
    }

    public async System.Threading.Tasks.Task DeleteAsync(Guid commentId)
    {
        var comment = await db.TaskComments.Include(c => c.Task).ThenInclude(t => t.Broad).SingleOrDefaultAsync(c => c.Id == commentId) ?? throw new KeyNotFoundException("Comment not found");
        if (comment.Task.Broad.AccountId != AccountId() || comment.AuthorAccountId != AccountId()) throw new UnauthorizedAccessException();
        db.TaskComments.Remove(comment); await db.SaveChangesAsync(); await github.SyncLocalCommentAsync(comment.Id, true);
    }
}
