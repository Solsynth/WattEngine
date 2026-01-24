using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using DysonNetwork.Shared.Models;

namespace WattEngine.Ideask.Broad;

public class WtProject : ModelBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = null!;
    [JsonIgnore] public ICollection<WtProjectMember> Members { get; set; } = new List<WtProjectMember>();
    [JsonIgnore] public ICollection<WtBroad> Broads { get; set; } = new List<WtBroad>();

    public Guid AccountId { get; set; }
    [NotMapped] public SnAccount? Account { get; set; }
}

public class WtProjectMember : ModelBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Permission Permission { get; set; }
    public bool IsCreator { get; set; }
    
    public Guid ProjectId { get; set; }
    [JsonIgnore] public WtProject Project { get; set; } = null!;
    public Guid AccountId { get; set; }
    [NotMapped] public SnAccount? Account { get; set; }
}