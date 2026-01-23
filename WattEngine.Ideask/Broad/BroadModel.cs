using System;
using System.Text.Json.Serialization;
using DysonNetwork.Shared.Models;

namespace WattEngine.Ideask.Broad;

public enum Permission
{
    Owner,
    Editor,
    Viewer
}

public class WtBroad : ModelBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = null!;
    public Guid AccountId { get; set; }
    public Guid? ProjectId { get; set; }
    [JsonIgnore]
    public WtProject? Project { get; set; }
}