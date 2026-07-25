using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using WattEngine.Ideask.Task;
using Task = System.Threading.Tasks.Task;

namespace WattEngine.Ideask.GitHub;

public class GitHubIntegrationService(
    AppDatabase db,
    IHttpContextAccessor httpContextAccessor,
    GitHubApiClient github,
    IConfiguration configuration,
    ILogger<GitHubIntegrationService> logger)
{
    private Guid CurrentAccountId() => (httpContextAccessor.HttpContext?.Items["CurrentUser"] as DysonNetwork.Shared.Models.SnAccount)?.Id
        ?? throw new UnauthorizedAccessException("User not authenticated");

    private System.Threading.Tasks.Task<string> TokenAsync(WtGitHubIntegration integration, CancellationToken ct = default) =>
        github.GetInstallationTokenAsync(integration.InstallationId, ct);

    private async System.Threading.Tasks.Task<WtGitHubIntegration> OwnedIntegrationAsync(Guid broadId)
    {
        var integration = await db.GitHubIntegrations.Include(i => i.Broad).SingleOrDefaultAsync(i => i.BroadId == broadId)
            ?? throw new KeyNotFoundException("GitHub repository is not linked to this broad.");
        if (integration.Broad.AccountId != CurrentAccountId()) throw new UnauthorizedAccessException();
        return integration;
    }

    private async System.Threading.Tasks.Task EnsureInstallationGrantAsync(Guid broadId, long installationId, CancellationToken ct)
    {
        if (!await db.GitHubInstallationGrants.AnyAsync(g => g.BroadId == broadId && g.AccountId == CurrentAccountId() && g.InstallationId == installationId && g.ExpiresAt > SystemClock.Instance.GetCurrentInstant(), ct))
            throw new UnauthorizedAccessException("This GitHub App installation is not authorized for this broad.");
    }

    public async System.Threading.Tasks.Task<string> CreateInstallUrlAsync(Guid broadId, CancellationToken ct = default)
    {
        var accountId = CurrentAccountId();
        if (!await db.Broads.AnyAsync(b => b.Id == broadId && b.AccountId == accountId, ct)) throw new KeyNotFoundException("Broad not found");
        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        db.GitHubInstallationGrants.Add(new WtGitHubInstallationGrant { BroadId = broadId, AccountId = accountId, State = state, ExpiresAt = SystemClock.Instance.GetCurrentInstant() + Duration.FromMinutes(15) });
        await db.SaveChangesAsync(ct);
        var slug = configuration["GitHub:AppSlug"] ?? throw new InvalidOperationException("GitHub:AppSlug must be configured.");
        return $"https://github.com/apps/{slug}/installations/new?state={state}";
    }

    public async System.Threading.Tasks.Task CompleteInstallationAsync(long installationId, string state, CancellationToken ct = default)
    {
        var grant = await db.GitHubInstallationGrants.SingleOrDefaultAsync(g => g.State == state, ct);
        if (grant is null || grant.ExpiresAt <= SystemClock.Instance.GetCurrentInstant()) throw new InvalidOperationException("The GitHub App installation request has expired.");
        _ = await github.GetInstallationTokenAsync(installationId, ct);
        grant.InstallationId = installationId;
        await db.SaveChangesAsync(ct);
    }

    public async System.Threading.Tasks.Task<List<GitHubRepository>> ListRepositoriesAsync(Guid broadId, long installationId, CancellationToken ct = default)
    {
        await EnsureInstallationGrantAsync(broadId, installationId, ct);
        return await github.ListInstallationRepositoriesAsync(await github.GetInstallationTokenAsync(installationId, ct), ct);
    }

    public async System.Threading.Tasks.Task<long?> GetCompletedInstallationAsync(Guid broadId, CancellationToken ct = default)
    {
        var accountId = CurrentAccountId();
        if (!await db.Broads.AnyAsync(b => b.Id == broadId && b.AccountId == accountId, ct)) throw new KeyNotFoundException("Broad not found");
        return await db.GitHubInstallationGrants
            .Where(g => g.BroadId == broadId && g.AccountId == accountId && g.ExpiresAt > SystemClock.Instance.GetCurrentInstant() && g.InstallationId != null)
            .OrderByDescending(g => g.UpdatedAt).Select(g => g.InstallationId).FirstOrDefaultAsync(ct);
    }

    public async System.Threading.Tasks.Task<WtGitHubIntegration> LinkAsync(Guid broadId, long installationId, string owner, string repository, CancellationToken ct = default)
    {
        var accountId = CurrentAccountId();
        var broad = await db.Broads.SingleOrDefaultAsync(b => b.Id == broadId) ?? throw new KeyNotFoundException("Broad not found");
        if (broad.AccountId != accountId) throw new UnauthorizedAccessException();
        await EnsureInstallationGrantAsync(broadId, installationId, ct);
        if (await db.GitHubIntegrations.AnyAsync(i => i.BroadId == broadId, ct)) throw new InvalidOperationException("This broad already has a linked repository.");
        var token = await github.GetInstallationTokenAsync(installationId, ct);
        var repo = await github.GetRepositoryAsync(token, owner, repository, ct);
        if (await db.GitHubIntegrations.AnyAsync(i => i.GitHubRepositoryId == repo.Id, ct)) throw new InvalidOperationException("That repository is already linked to another broad.");
        var integration = new WtGitHubIntegration { BroadId = broadId, InstallationId = installationId, GitHubRepositoryId = repo.Id, Owner = repo.Owner, Repository = repo.Name };
        db.GitHubIntegrations.Add(integration);
        await db.SaveChangesAsync(ct);
        var saved = await db.GitHubIntegrations.Include(i => i.Broad).SingleAsync(i => i.Id == integration.Id, ct);
        await ReconcileAsync(saved, ct);
        return saved;
    }

    public async System.Threading.Tasks.Task UnlinkAsync(Guid broadId, CancellationToken ct = default)
    {
        var integration = await OwnedIntegrationAsync(broadId);
        db.GitHubIntegrations.Remove(integration);
        await db.SaveChangesAsync(ct);
    }

    public async System.Threading.Tasks.Task ReconcileAsync(Guid broadId, CancellationToken ct = default) => await ReconcileAsync(await OwnedIntegrationAsync(broadId), ct);

    public async System.Threading.Tasks.Task<WtGitHubIntegration> GetStatusAsync(Guid broadId) => await OwnedIntegrationAsync(broadId);

    public async System.Threading.Tasks.Task ReconcileAsync(WtGitHubIntegration integration, CancellationToken ct = default)
    {
        try
        {
            var token = await TokenAsync(integration, ct);
            foreach (var issue in await github.ListIssuesAsync(token, integration.Owner, integration.Repository, ct))
                if (!issue.IsPullRequest) await ApplyIssueAsync(integration, issue, token, ct);
            integration.LastSyncedAt = SystemClock.Instance.GetCurrentInstant();
            integration.LastError = null;
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            integration.LastError = ex.Message[..Math.Min(ex.Message.Length, 4096)];
            await db.SaveChangesAsync(ct);
            logger.LogWarning(ex, "GitHub reconciliation failed for integration {IntegrationId}", integration.Id);
            throw;
        }
    }

    public async System.Threading.Tasks.Task ReconcileAllAsync(CancellationToken ct = default)
    {
        var integrations = await db.GitHubIntegrations.Include(i => i.Broad).ToListAsync(ct);
        foreach (var integration in integrations)
        {
            try { await ReconcileAsync(integration, ct); }
            catch (Exception ex) { logger.LogWarning(ex, "GitHub scheduled reconciliation failed for {IntegrationId}", integration.Id); }
        }
    }

    public async System.Threading.Tasks.Task SyncLocalTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        try
        {
            var task = await db.Tasks.Include(t => t.Broad).SingleOrDefaultAsync(t => t.Id == taskId, ct);
            if (task is null) return;
            var integration = await db.GitHubIntegrations.SingleOrDefaultAsync(i => i.BroadId == task.BroadId, ct);
            if (integration is null) return;
            var token = await TokenAsync(integration, ct);
            var link = await db.GitHubIssueLinks.SingleOrDefaultAsync(l => l.TaskId == taskId, ct);
            if (link is null)
            {
                var issue = await github.CreateIssueAsync(token, integration.Owner, integration.Repository, task.Name, task.Content, task.Tags, ct);
                db.GitHubIssueLinks.Add(new WtGitHubIssueLink { IntegrationId = integration.Id, TaskId = task.Id, GitHubIssueId = issue.Id, IssueNumber = issue.Number, HtmlUrl = issue.HtmlUrl, LastGitHubUpdatedAt = issue.UpdatedAt });
                await db.SaveChangesAsync(ct);
            }
            else await github.UpdateIssueAsync(token, integration, link.IssueNumber, task.Name, task.Content, task.Tags, task.CompleteReason.HasValue, ct);
        }
        catch (Exception ex) { logger.LogWarning(ex, "GitHub task sync failed for {TaskId}", taskId); }
    }

    public async System.Threading.Tasks.Task SyncLocalCommentAsync(Guid commentId, bool deleted = false, CancellationToken ct = default)
    {
        try
        {
            var comment = await db.TaskComments.IgnoreQueryFilters().Include(c => c.Task).ThenInclude(t => t.Broad).Include(c => c.GitHubComment).SingleOrDefaultAsync(c => c.Id == commentId, ct);
            if (comment is null) return;
            var integration = await db.GitHubIntegrations.SingleOrDefaultAsync(i => i.BroadId == comment.Task.BroadId, ct);
            var issue = await db.GitHubIssueLinks.SingleOrDefaultAsync(l => l.TaskId == comment.TaskId, ct);
            if (integration is null || issue is null) return;
            var token = await TokenAsync(integration, ct);
            if (deleted && comment.GitHubComment is not null) await github.DeleteCommentAsync(token, integration, comment.GitHubComment.GitHubCommentId, ct);
            else if (comment.GitHubComment is null)
            {
                var remoteId = await github.CreateCommentAsync(token, integration, issue.IssueNumber, comment.Content, ct);
                db.GitHubCommentLinks.Add(new WtGitHubCommentLink { CommentId = comment.Id, GitHubCommentId = remoteId });
                await db.SaveChangesAsync(ct);
            }
            else await github.UpdateCommentAsync(token, integration, comment.GitHubComment.GitHubCommentId, comment.Content, ct);
        }
        catch (Exception ex) { logger.LogWarning(ex, "GitHub comment sync failed for {CommentId}", commentId); }
    }

    private async System.Threading.Tasks.Task ApplyIssueAsync(WtGitHubIntegration integration, GitHubIssue issue, string token, CancellationToken ct)
    {
        var link = await db.GitHubIssueLinks.Include(l => l.Task).SingleOrDefaultAsync(l => l.IntegrationId == integration.Id && l.GitHubIssueId == issue.Id, ct);
        WtTask task;
        if (link is null)
        {
            task = new WtTask { BroadId = integration.BroadId, Name = issue.Title, Content = issue.Body, Tags = issue.Labels, CompleteReason = issue.State == "closed" ? TaskCompleteReason.Completed : null, CompletedAt = issue.State == "closed" ? SystemClock.Instance.GetCurrentInstant() : null };
            db.Tasks.Add(task);
            link = new WtGitHubIssueLink { IntegrationId = integration.Id, Task = task, GitHubIssueId = issue.Id, IssueNumber = issue.Number, HtmlUrl = issue.HtmlUrl, LastGitHubUpdatedAt = issue.UpdatedAt };
            db.GitHubIssueLinks.Add(link);
        }
        else if (!link.LastGitHubUpdatedAt.HasValue || !issue.UpdatedAt.HasValue || issue.UpdatedAt >= link.LastGitHubUpdatedAt)
        {
            task = link.Task;
            task.Name = issue.Title; task.Content = issue.Body; task.Tags = issue.Labels;
            task.CompleteReason = issue.State == "closed" ? TaskCompleteReason.Completed : null;
            task.CompletedAt = issue.State == "closed" ? SystemClock.Instance.GetCurrentInstant() : null;
            link.IssueNumber = issue.Number; link.HtmlUrl = issue.HtmlUrl; link.LastGitHubUpdatedAt = issue.UpdatedAt;
        }
        else return;
        await db.SaveChangesAsync(ct);
        foreach (var comment in await github.ListCommentsAsync(token, integration, issue.Number, ct))
            await ApplyCommentAsync(task, comment, ct);
        await db.SaveChangesAsync(ct);
    }

    private async System.Threading.Tasks.Task ApplyCommentAsync(WtTask task, GitHubComment comment, CancellationToken ct)
    {
        var existing = await db.GitHubCommentLinks.Include(l => l.Comment).SingleOrDefaultAsync(l => l.GitHubCommentId == comment.Id, ct);
        if (existing is not null)
        {
            existing.Comment.Content = comment.Body;
            existing.Comment.ExternalAuthorLogin = comment.Login;
            existing.Comment.ExternalAuthorAvatarUrl = comment.AvatarUrl;
            return;
        }
        var local = new WtTaskComment { TaskId = task.Id, Content = comment.Body, ExternalAuthorLogin = comment.Login, ExternalAuthorAvatarUrl = comment.AvatarUrl };
        db.TaskComments.Add(local); db.GitHubCommentLinks.Add(new WtGitHubCommentLink { Comment = local, GitHubCommentId = comment.Id });
    }

    public async System.Threading.Tasks.Task HandleWebhookAsync(long repositoryId, GitHubIssue? issue, GitHubComment? comment, string action, CancellationToken ct = default)
    {
        var integration = await db.GitHubIntegrations.Include(i => i.Broad).SingleOrDefaultAsync(i => i.GitHubRepositoryId == repositoryId, ct);
        if (integration is null) return;
        var token = await TokenAsync(integration, ct);
        if (issue is not null && !issue.IsPullRequest) await ApplyIssueAsync(integration, issue, token, ct);
        if (comment is not null && issue is not null && action is not "deleted")
        {
            var link = await db.GitHubIssueLinks.Include(l => l.Task).SingleOrDefaultAsync(l => l.IntegrationId == integration.Id && l.GitHubIssueId == issue.Id, ct);
            if (link is not null) { await ApplyCommentAsync(link.Task, comment, ct); await db.SaveChangesAsync(ct); }
        }
        else if (comment is not null && action == "deleted")
        {
            var commentLink = await db.GitHubCommentLinks.Include(l => l.Comment).SingleOrDefaultAsync(l => l.GitHubCommentId == comment.Id, ct);
            if (commentLink is not null) { db.TaskComments.Remove(commentLink.Comment); await db.SaveChangesAsync(ct); }
        }
    }

    public System.Threading.Tasks.Task<bool> VerifySignatureAsync(string signature, byte[] body)
    {
        if (!signature.StartsWith("sha256=", StringComparison.Ordinal)) return System.Threading.Tasks.Task.FromResult(false);
        var secret = configuration["GitHub:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret)) return System.Threading.Tasks.Task.FromResult(false);
        var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body)).ToLowerInvariant();
        return System.Threading.Tasks.Task.FromResult(CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(signature[7..])));
    }

    public async System.Threading.Tasks.Task RemoveForRepositoryAsync(long repositoryId, CancellationToken ct = default)
    {
        var integration = await db.GitHubIntegrations.SingleOrDefaultAsync(i => i.GitHubRepositoryId == repositoryId, ct);
        if (integration is null) return;
        db.GitHubIntegrations.Remove(integration);
        await db.SaveChangesAsync(ct);
    }

    public async System.Threading.Tasks.Task RemoveForInstallationAsync(long installationId, CancellationToken ct = default)
    {
        var integrations = await db.GitHubIntegrations.Where(i => i.InstallationId == installationId).ToListAsync(ct);
        db.GitHubIntegrations.RemoveRange(integrations);
        await db.SaveChangesAsync(ct);
    }
}
