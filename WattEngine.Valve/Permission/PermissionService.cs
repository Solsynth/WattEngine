using DysonNetwork.Shared.Cache;
using Microsoft.EntityFrameworkCore;

namespace WattEngine.Valve.Workspace;

public class PermissionService(
    AppDatabase db,
    WorkspaceService ws,
    ICacheService cache
)
{
    private const string CacheKeyPrefix = "workspace:perm:";

    public async Task<bool> HasPermission(Guid workspaceId, Guid accountId, string permission)
    {
        // Check user-specific override first
        var userPermission = await db.WorkspaceUserPermissions
            .FirstOrDefaultAsync(p => p.WorkspaceId == workspaceId && p.AccountId == accountId && p.DeletedAt == null);

        if (userPermission != null)
        {
            var userValue = GetPermissionValue(userPermission, permission);
            if (userValue.HasValue)
                return userValue.Value;
        }

        // Fall back to role permission
        return await GetRolePermission(workspaceId, accountId, permission);
    }

    public async Task<WtWorkspaceRolePermission?> GetRolePermissions(Guid workspaceId, int roleLevel)
    {
        return await db.WorkspaceRolePermissions
            .FirstOrDefaultAsync(p => p.WorkspaceId == workspaceId && p.RoleLevel == roleLevel && p.DeletedAt == null);
    }

    public async Task<List<WtWorkspaceRolePermission>> GetAllRolePermissions(Guid workspaceId)
    {
        return await db.WorkspaceRolePermissions
            .Where(p => p.WorkspaceId == workspaceId && p.DeletedAt == null)
            .OrderBy(p => p.RoleLevel)
            .ToListAsync();
    }

    public async Task<WtWorkspaceRolePermission> UpdateRolePermission(Guid workspaceId, int roleLevel, WtWorkspaceRolePermission permission)
    {
        var existing = await db.WorkspaceRolePermissions
            .FirstOrDefaultAsync(p => p.WorkspaceId == workspaceId && p.RoleLevel == roleLevel && p.DeletedAt == null);

        if (existing != null)
        {
            existing.CanManageWorkspace = permission.CanManageWorkspace;
            existing.CanManageMembers = permission.CanManageMembers;
            existing.CanManageBilling = permission.CanManageBilling;
            existing.CanCreateProjects = permission.CanCreateProjects;
            existing.CanManageProjects = permission.CanManageProjects;
            existing.CanUseIdeask = permission.CanUseIdeask;
            existing.CanUseDrive = permission.CanUseDrive;
            db.WorkspaceRolePermissions.Update(existing);
        }
        else
        {
            permission.WorkspaceId = workspaceId;
            permission.RoleLevel = roleLevel;
            db.WorkspaceRolePermissions.Add(permission);
            existing = permission;
        }

        await db.SaveChangesAsync();
        return existing;
    }

    public async Task<WtWorkspaceUserPermission?> GetUserPermission(Guid workspaceId, Guid accountId)
    {
        return await db.WorkspaceUserPermissions
            .FirstOrDefaultAsync(p => p.WorkspaceId == workspaceId && p.AccountId == accountId && p.DeletedAt == null);
    }

    public async Task<WtWorkspaceUserPermission> UpdateUserPermission(Guid workspaceId, Guid accountId, WtWorkspaceUserPermission permission)
    {
        var existing = await db.WorkspaceUserPermissions
            .FirstOrDefaultAsync(p => p.WorkspaceId == workspaceId && p.AccountId == accountId && p.DeletedAt == null);

        if (existing != null)
        {
            existing.CanManageWorkspace = permission.CanManageWorkspace;
            existing.CanManageMembers = permission.CanManageMembers;
            existing.CanManageBilling = permission.CanManageBilling;
            existing.CanCreateProjects = permission.CanCreateProjects;
            existing.CanManageProjects = permission.CanManageProjects;
            existing.CanUseIdeask = permission.CanUseIdeask;
            existing.CanUseDrive = permission.CanUseDrive;
            db.WorkspaceUserPermissions.Update(existing);
        }
        else
        {
            permission.WorkspaceId = workspaceId;
            permission.AccountId = accountId;
            db.WorkspaceUserPermissions.Add(permission);
            existing = permission;
        }

        await db.SaveChangesAsync();
        return existing;
    }

    private async Task<bool> GetRolePermission(Guid workspaceId, Guid accountId, string permission)
    {
        var member = await ws.GetMember(workspaceId, accountId);
        if (member == null) return false;

        var rolePermission = await db.WorkspaceRolePermissions
            .FirstOrDefaultAsync(p => p.WorkspaceId == workspaceId && p.RoleLevel == member.Role && p.DeletedAt == null);

        if (rolePermission != null)
        {
            return GetPermissionValue(rolePermission, permission);
        }

        return GetDefaultPermission(member.Role, permission);
    }

    private static bool GetPermissionValue(WtWorkspaceRolePermission permission, string key) => key switch
    {
        "workspace.manage" => permission.CanManageWorkspace,
        "workspace.members" => permission.CanManageMembers,
        "workspace.billing" => permission.CanManageBilling,
        "projects.create" => permission.CanCreateProjects,
        "projects.manage" => permission.CanManageProjects,
        "ideask.use" => permission.CanUseIdeask,
        "drive.use" => permission.CanUseDrive,
        _ => false
    };

    private static bool? GetPermissionValue(WtWorkspaceUserPermission permission, string key) => key switch
    {
        "workspace.manage" => permission.CanManageWorkspace,
        "workspace.members" => permission.CanManageMembers,
        "workspace.billing" => permission.CanManageBilling,
        "projects.create" => permission.CanCreateProjects,
        "projects.manage" => permission.CanManageProjects,
        "ideask.use" => permission.CanUseIdeask,
        "drive.use" => permission.CanUseDrive,
        _ => null
    };

    public static bool GetDefaultPermission(int role, string permission)
    {
        return role switch
        {
            WorkspaceMemberRole.Owner => true,
            WorkspaceMemberRole.Admin => permission switch
            {
                "workspace.manage" => false,
                "workspace.members" => true,
                "workspace.billing" => false,
                "projects.create" => true,
                "projects.manage" => true,
                "ideask.use" => true,
                "drive.use" => true,
                _ => false
            },
            WorkspaceMemberRole.Member => permission switch
            {
                "workspace.manage" => false,
                "workspace.members" => false,
                "workspace.billing" => false,
                "projects.create" => true,
                "projects.manage" => false,
                "ideask.use" => true,
                "drive.use" => true,
                _ => false
            },
            WorkspaceMemberRole.Viewer => permission switch
            {
                "ideask.use" => true,
                "drive.use" => true,
                _ => false
            },
            _ => false
        };
    }
}
