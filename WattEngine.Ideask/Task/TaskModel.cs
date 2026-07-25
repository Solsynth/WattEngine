using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using DysonNetwork.Shared.Models;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using WattEngine.Ideask.Broad;
using WattEngine.Ideask.GitHub;

namespace WattEngine.Ideask.Task;

public enum TaskCompleteReason
{
    Completed,
    Skipped,
    Duplicated
}

public class WtTask : ModelBase
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(4096)] public string Name { get; set; } = null!;
    [MaxLength(8192)] public string? Description { get; set; }
    [Column(TypeName = "text")] public string? Content { get; set; }
    [Column(TypeName = "jsonb")] public List<SnCloudFileReferenceObject> Attachments { get; set; } = [];
    [Column(TypeName = "jsonb")] public List<string> Tags { get; set; } = [];

    public int Priority { get; set; }

    public Instant? DeadlineAt { get; set; }
    public Instant? CompletedAt { get; set; }
    public TaskCompleteReason? CompleteReason { get; set; }

    public Guid BroadId { get; set; }
    [JsonIgnore]
    public WtBroad Broad { get; set; } = null!;

    public Guid? GroupId { get; set; }
    [JsonIgnore] public WtTaskGroup? Group { get; set; }

    public Guid? ParentTaskId { get; set; }
    [JsonIgnore]
    public WtTask? ParentTask { get; set; }
    [JsonIgnore]
    public ICollection<WtTask> SubTasks { get; set; } = new List<WtTask>();
    [JsonIgnore]
    public ICollection<WtTaskAssignee> Assignees { get; set; } = new List<WtTaskAssignee>();
    [JsonIgnore] public ICollection<WtTaskComment> Comments { get; set; } = new List<WtTaskComment>();
    public WtGitHubIssueLink? GitHubIssue { get; set; }
}

[Index(nameof(BroadId), nameof(Position))]
public class WtTaskGroup : ModelBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BroadId { get; set; }
    [JsonIgnore] public WtBroad Broad { get; set; } = null!;
    [Required, MaxLength(256)] public string Name { get; set; } = null!;
    public int Position { get; set; }
    [JsonIgnore] public ICollection<WtTask> Tasks { get; set; } = new List<WtTask>();
}

[Index(nameof(TaskId))]
[Index(nameof(AccountId))]
public class WtTaskAssignee : ModelBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    [JsonIgnore] public WtTask Task { get; set; } = null!;
    public Guid AccountId { get; set; }
}

[Index(nameof(TaskId), nameof(CreatedAt))]
public class WtTaskComment : ModelBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    [JsonIgnore] public WtTask Task { get; set; } = null!;
    public Guid? AuthorAccountId { get; set; }
    [MaxLength(256)] public string? ExternalAuthorLogin { get; set; }
    [MaxLength(2048)] public string? ExternalAuthorAvatarUrl { get; set; }
    [Required, Column(TypeName = "text")] public string Content { get; set; } = null!;
    public WtGitHubCommentLink? GitHubComment { get; set; }
}
