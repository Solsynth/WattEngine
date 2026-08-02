using DysonNetwork.Shared.Auth;
using DysonNetwork.Shared.Capabilities;
using DysonNetwork.Shared.Networking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using WattEngine.Ideask.GitHub;

namespace WattEngine.Ideask.Admin;

/// <summary>
/// Platform-level GitHub integration administration for diagnosing sync failures
/// and removing misbehaving repository links. Gated by the
/// <see cref="PermissionKeys.AdminTasksIntegrationsManage"/> permission node.
/// </summary>
[ApiController]
[Route("/api/admin/github-integrations")]
[Authorize]
[ApiFeature("admin.github-integrations", Revision = 1)]
public class GitHubIntegrationAdminController(AppDatabase db) : ControllerBase
{
    public class GitHubIntegrationAdminSummary
    {
        public Guid Id { get; set; }
        public Guid BroadId { get; set; }
        public long InstallationId { get; set; }
        public long GitHubRepositoryId { get; set; }
        public string Owner { get; set; } = string.Empty;
        public string Repository { get; set; } = string.Empty;
        public Instant? LastSyncedAt { get; set; }
        public string? LastError { get; set; }
        public Instant? DeletedAt { get; set; }
        public Instant CreatedAt { get; set; }
        public Instant UpdatedAt { get; set; }
    }

    [HttpGet]
    [AskPermission(PermissionKeys.AdminTasksIntegrationsManage)]
    public async Task<ActionResult<List<GitHubIntegrationAdminSummary>>> ListIntegrations(
        [FromQuery] Guid? broadId = null,
        [FromQuery] string? q = null,
        [FromQuery] bool includeDeleted = false,
        [FromQuery] int take = 50,
        [FromQuery] int offset = 0,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);
        offset = Math.Max(offset, 0);

        var query = db.GitHubIntegrations.AsNoTracking();
        if (includeDeleted)
            query = query.IgnoreQueryFilters();
        else
            query = query.Where(i => i.DeletedAt == null);

        if (broadId.HasValue) query = query.Where(i => i.BroadId == broadId.Value);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var probe = q.Trim();
            query = query.Where(i =>
                EF.Functions.ILike(i.Owner, $"%{probe}%") ||
                EF.Functions.ILike(i.Repository, $"%{probe}%"));
        }

        var total = await query.CountAsync(ct);
        Response.Headers.Append("X-Total", total.ToString());

        return Ok(await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip(offset)
            .Take(take)
            .Select(i => new GitHubIntegrationAdminSummary
            {
                Id = i.Id,
                BroadId = i.BroadId,
                InstallationId = i.InstallationId,
                GitHubRepositoryId = i.GitHubRepositoryId,
                Owner = i.Owner,
                Repository = i.Repository,
                LastSyncedAt = i.LastSyncedAt,
                LastError = i.LastError,
                DeletedAt = i.DeletedAt,
                CreatedAt = i.CreatedAt,
                UpdatedAt = i.UpdatedAt
            })
            .ToListAsync(ct));
    }

    [HttpDelete("{id:guid}")]
    [AskPermission(PermissionKeys.AdminTasksIntegrationsManage)]
    public async Task<IActionResult> DeleteIntegration(Guid id, CancellationToken ct)
    {
        var integration = await db.GitHubIntegrations.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (integration is null)
            return NotFound(ApiError.NotFound("github integration", code: "GITHUB_INTEGRATION_NOT_FOUND"));

        var issueLinks = await db.GitHubIssueLinks.Where(l => l.IntegrationId == id).ToListAsync(ct);
        var commentLinks = await db.GitHubCommentLinks.Where(l => l.IntegrationId == id).ToListAsync(ct);

        db.GitHubCommentLinks.RemoveRange(commentLinks);
        db.GitHubIssueLinks.RemoveRange(issueLinks);
        db.GitHubIntegrations.Remove(integration);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
