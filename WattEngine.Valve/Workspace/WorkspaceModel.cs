using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using DysonNetwork.Shared.Models;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace WattEngine.Valve.Workspace;

public enum WorkspaceType
{
    Individual = 0,
    Organization = 1
}

public enum WorkspacePlan
{
    Free = 0,
    Pro = 1,
    Enterprise = 2
}

[Index(nameof(Slug), nameof(DeletedAt), IsUnique = true)]
public class WtWorkspace : ModelBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(1024)] public string Slug { get; set; } = string.Empty;
    [MaxLength(1024)] public string Name { get; set; } = string.Empty;
    [MaxLength(4096)] public string? Description { get; set; }
    public WorkspaceType Type { get; set; } = WorkspaceType.Individual;
    public Guid OwnerAccountId { get; set; }

    [Column(TypeName = "jsonb")] public SnCloudFileReferenceObject? Picture { get; set; }
    [Column(TypeName = "jsonb")] public SnCloudFileReferenceObject? Background { get; set; }

    public WorkspacePlan Plan { get; set; } = WorkspacePlan.Free;
    public Instant? PlanExpiresAt { get; set; }
    public Guid? ActiveOrderId { get; set; }
    public bool IsBundled { get; set; }

    [JsonIgnore] public List<WtWorkspaceMember> Members { get; set; } = [];
    [JsonIgnore] public List<WtWorkspaceRolePermission> RolePermissions { get; set; } = [];
    [JsonIgnore] public List<WtWorkspaceUserPermission> UserPermissions { get; set; } = [];
    [JsonIgnore] public List<WtWorkspaceBundledPlan> BundledPlans { get; set; } = [];
}

public static class WorkspaceMemberRole
{
    public const int Owner = 100;
    public const int Admin = 75;
    public const int Member = 50;
    public const int Viewer = 25;
}

public class WtWorkspaceMember : ModelBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    [JsonIgnore] public WtWorkspace Workspace { get; set; } = null!;
    public Guid AccountId { get; set; }
    [NotMapped, JsonIgnore] public SnAccount? Account { get; set; }
    public int Role { get; set; } = WorkspaceMemberRole.Viewer;
    public Instant? JoinedAt { get; set; }
    public Instant? LeaveAt { get; set; }
}

public class WtWorkspaceRolePermission : ModelBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    [JsonIgnore] public WtWorkspace Workspace { get; set; } = null!;
    public int RoleLevel { get; set; } = WorkspaceMemberRole.Viewer;

    // Workspace management
    public bool CanManageWorkspace { get; set; }
    public bool CanManageMembers { get; set; }
    public bool CanManageBilling { get; set; }

    // Service permissions
    public bool CanCreateProjects { get; set; } = true;
    public bool CanManageProjects { get; set; }
    public bool CanUseIdeask { get; set; } = true;
    public bool CanUseDrive { get; set; } = true;
}

public class WtWorkspaceUserPermission : ModelBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    [JsonIgnore] public WtWorkspace Workspace { get; set; } = null!;
    public Guid AccountId { get; set; }

    // Nullable overrides (null = use role default)
    public bool? CanManageWorkspace { get; set; }
    public bool? CanManageMembers { get; set; }
    public bool? CanManageBilling { get; set; }
    public bool? CanCreateProjects { get; set; }
    public bool? CanManageProjects { get; set; }
    public bool? CanUseIdeask { get; set; }
    public bool? CanUseDrive { get; set; }
}

/// Plan-based quotas for workspace resource limits
public static class WorkspacePlanQuota
{
    public static int GetMaxProjects(WorkspacePlan plan) => plan switch
    {
        WorkspacePlan.Free => 3,
        WorkspacePlan.Pro => 20,
        WorkspacePlan.Enterprise => 100,
        _ => 3
    };

    public static int GetMaxMembers(WorkspacePlan plan) => plan switch
    {
        WorkspacePlan.Free => 5,
        WorkspacePlan.Pro => 50,
        WorkspacePlan.Enterprise => 500,
        _ => 5
    };

    public static int GetMaxTasksPerProject(WorkspacePlan plan) => plan switch
    {
        WorkspacePlan.Free => 100,
        WorkspacePlan.Pro => 1000,
        WorkspacePlan.Enterprise => 10000,
        _ => 100
    };

    public static int GetMaxBroadsPerProject(WorkspacePlan plan) => plan switch
    {
        WorkspacePlan.Free => 5,
        WorkspacePlan.Pro => 50,
        WorkspacePlan.Enterprise => 200,
        _ => 5
    };

    public static long GetMaxStorageBytes(WorkspacePlan plan) => plan switch
    {
        WorkspacePlan.Free => 1024L * 1024 * 1024,           // 1 GB
        WorkspacePlan.Pro => 10L * 1024 * 1024 * 1024,       // 10 GB
        WorkspacePlan.Enterprise => 100L * 1024 * 1024 * 1024, // 100 GB
        _ => 1024L * 1024 * 1024
    };
}

public static class WorkspacePlanPricing
{
    public const string ProductIdentifierPro = "watt.workspace.plan.pro";
    public const string ProductIdentifierEnterprise = "watt.workspace.plan.enterprise";
    public static readonly Duration ReassignCooldown = Duration.FromDays(7);
    public const int BundledPlanRequiredPerkLevel = 3;

    public static decimal GetMonthlyPrice(WorkspacePlan plan) => plan switch
    {
        WorkspacePlan.Pro => 100m,
        WorkspacePlan.Enterprise => 500m,
        _ => 0m
    };
}

[Index(nameof(AccountId))]
[Index(nameof(WorkspaceId))]
public class WtWorkspaceBundledPlan : ModelBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public Guid WorkspaceId { get; set; }
    [JsonIgnore] public WtWorkspace Workspace { get; set; } = null!;
    public bool IsEnabled { get; set; } = true;
    public Instant? DisabledAt { get; set; }
    public Instant? LastReassignedAt { get; set; }
}
