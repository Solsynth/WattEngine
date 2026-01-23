using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using DysonNetwork.Shared.Models;

namespace WattEngine.Ideask.Broad;

public class WtProject : ModelBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = null!;
    public Guid CreatorAccountId { get; set; }
    [JsonIgnore]
    public ICollection<WtProjectMember> Members { get; set; } = new List<WtProjectMember>();
    [JsonIgnore]
    public ICollection<WtBroad> Broads { get; set; } = new List<WtBroad>();
}

public class WtProjectMember : ModelBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    [JsonIgnore]
    public WtProject Project { get; set; } = null!;
    public Guid AccountId { get; set; }
    public Permission Permission { get; set; }
    public bool IsCreator { get; set; }
}