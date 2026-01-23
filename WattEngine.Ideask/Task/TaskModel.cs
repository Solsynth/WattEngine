using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using DysonNetwork.Shared.Models;
using NodaTime;
using WattEngine.Ideask.Broad;

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
    // ReSharper disable once EntityFramework.ModelValidation.UnlimitedStringLength
    [Column(TypeName = "text")] public string? Content { get; set; }
    [Column(TypeName = "jsonb")] public List<SnCloudFileReferenceObject> Attachments { get; set; } = [];
    
    public int Priority { get; set; }
 
    public Instant? DeadlineAt { get; set; }
    public Instant? CompletedAt { get; set; }
    public TaskCompleteReason? CompleteReason { get; set; }

    public Guid BroadId { get; set; }
    [JsonIgnore]
    public WtBroad Broad { get; set; } = null!;

    public Guid? ParentTaskId { get; set; }
    [JsonIgnore]
    public WtTask? ParentTask { get; set; }
    [JsonIgnore]
    public ICollection<WtTask> SubTasks { get; set; } = new List<WtTask>();
    [JsonIgnore]
    public ICollection<WtProjectMember> Assignees { get; set; } = new List<WtProjectMember>();
}