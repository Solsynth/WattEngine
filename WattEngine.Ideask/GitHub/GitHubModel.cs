using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using DysonNetwork.Shared.Models;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using WattEngine.Ideask.Broad;
using WattEngine.Ideask.Task;

namespace WattEngine.Ideask.GitHub;

[Index(nameof(BroadId))]
[Index(nameof(GitHubRepositoryId), IsUnique = true)]
public class WtGitHubIntegration : ModelBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BroadId { get; set; }
    [JsonIgnore] public WtBroad Broad { get; set; } = null!;
    public long InstallationId { get; set; }
    public long GitHubRepositoryId { get; set; }
    [MaxLength(256)] public string Owner { get; set; } = null!;
    [MaxLength(256)] public string Repository { get; set; } = null!;
    public Instant? LastSyncedAt { get; set; }
    [MaxLength(4096)] public string? LastError { get; set; }
}

[Index(nameof(State), IsUnique = true)]
[Index(nameof(WorkspaceId), nameof(AccountId))]
public class WtGitHubInstallationGrant : ModelBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? WorkspaceId { get; set; }
    public Guid AccountId { get; set; }
    [MaxLength(128)] public string State { get; set; } = null!;
    public long? InstallationId { get; set; }
    public Instant ExpiresAt { get; set; }
    public Instant? CompletedAt { get; set; }
}

[Index(nameof(IntegrationId), nameof(GitHubIssueId), IsUnique = true)]
[Index(nameof(IntegrationId), nameof(TaskId), IsUnique = true)]
public class WtGitHubIssueLink : ModelBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid IntegrationId { get; set; }
    [JsonIgnore] public WtGitHubIntegration Integration { get; set; } = null!;
    public Guid TaskId { get; set; }
    [JsonIgnore] public WtTask Task { get; set; } = null!;
    public long GitHubIssueId { get; set; }
    public int IssueNumber { get; set; }
    public bool IsPullRequest { get; set; }
    [NotMapped] public string? RepositoryFullName => Integration is null
        ? null
        : $"{Integration.Owner}/{Integration.Repository}";
    [MaxLength(2048)] public string HtmlUrl { get; set; } = null!;
    public Instant? LastGitHubUpdatedAt { get; set; }
}

[Index(nameof(IntegrationId), nameof(CommentId), IsUnique = true)]
[Index(nameof(GitHubCommentId), IsUnique = true)]
public class WtGitHubCommentLink : ModelBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CommentId { get; set; }
    [JsonIgnore] public WtTaskComment Comment { get; set; } = null!;
    public Guid IntegrationId { get; set; }
    public long GitHubCommentId { get; set; }
}
