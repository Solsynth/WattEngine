using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using DysonNetwork.Shared.Models;

namespace WattEngine.Ideask.Broad;

public enum Permission
{
    Owner,
    Editor,
    Viewer
}

public enum Visibility
{
    Private,
    Public,
    ProjectOnly
}

public class WtBroad : ModelBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = null!;
    public Guid AccountId { get; set; }
    public Guid? ProjectId { get; set; }
    [JsonIgnore]
    public WtProject? Project { get; set; }
    
    public Visibility Visibility { get; set; } = Visibility.Private;
    [MaxLength(8192)]
    public string? Description { get; set; }
    [Column(TypeName = "text")]
    public string? Content { get; set; }
    [Column(TypeName = "jsonb")]
    public SnCloudFileReferenceObject? BackgroundImage { get; set; }
    [Column(TypeName = "jsonb")]
    public SnCloudFileReferenceObject? IconImage { get; set; }
}
