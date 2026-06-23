using DysonNetwork.Shared.Cache;
using DysonNetwork.Shared.Models;
using DysonNetwork.Shared.Proto;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace WattEngine.Valve.Workspace;

public class WorkspaceService(
    AppDatabase db,
    ICacheService cache,
    DyAccountService.DyAccountServiceClient accountGrpc
)
{
    private const string CacheKeyPrefix = "workspace:";

    public async Task<WtWorkspace?> GetBySlug(string slug)
    {
        return await db.Workspaces
            .FirstOrDefaultAsync(w => w.Slug == slug && w.DeletedAt == null);
    }

    public async Task<WtWorkspace?> GetById(Guid id)
    {
        return await db.Workspaces
            .FirstOrDefaultAsync(w => w.Id == id && w.DeletedAt == null);
    }

    public async Task<List<WtWorkspace>> GetUserWorkspaces(Guid accountId)
    {
        var cacheKey = $"{CacheKeyPrefix}user:{accountId}";
        var (found, cached) = await cache.GetAsyncWithStatus<List<WtWorkspace>>(cacheKey);
        if (found && cached != null)
            return cached;

        var workspaces = await db.WorkspaceMembers
            .Where(m => m.AccountId == accountId && m.JoinedAt != null && m.LeaveAt == null)
            .Include(m => m.Workspace)
            .Select(m => m.Workspace)
            .Where(w => w.DeletedAt == null)
            .ToListAsync();

        await cache.SetAsync(cacheKey, workspaces, TimeSpan.FromMinutes(5));
        return workspaces;
    }

    public async Task<WtWorkspace> Create(WtWorkspace workspace, Guid creatorAccountId)
    {
        workspace.OwnerAccountId = creatorAccountId;
        db.Workspaces.Add(workspace);

        // Add creator as owner
        var member = new WtWorkspaceMember
        {
            WorkspaceId = workspace.Id,
            AccountId = creatorAccountId,
            Role = WorkspaceMemberRole.Owner,
            JoinedAt = SystemClock.Instance.GetCurrentInstant()
        };
        db.WorkspaceMembers.Add(member);

        // Add default role permissions
        await AddDefaultRolePermissions(workspace.Id);

        await db.SaveChangesAsync();
        await InvalidateUserCache(creatorAccountId);
        return workspace;
    }

    public async Task<WtWorkspace> Update(WtWorkspace workspace)
    {
        db.Workspaces.Update(workspace);
        await db.SaveChangesAsync();
        return workspace;
    }

    public async Task Delete(WtWorkspace workspace)
    {
        workspace.DeletedAt = SystemClock.Instance.GetCurrentInstant();
        db.Workspaces.Update(workspace);
        await db.SaveChangesAsync();
    }

    public async Task<WtWorkspaceMember?> GetMember(Guid workspaceId, Guid accountId)
    {
        return await db.WorkspaceMembers
            .FirstOrDefaultAsync(m =>
                m.WorkspaceId == workspaceId &&
                m.AccountId == accountId &&
                m.LeaveAt == null &&
                m.DeletedAt == null);
    }

    public async Task<List<WtWorkspaceMember>> GetMembers(Guid workspaceId)
    {
        return await db.WorkspaceMembers
            .Where(m => m.WorkspaceId == workspaceId && m.LeaveAt == null && m.DeletedAt == null)
            .ToListAsync();
    }

    public async Task<WtWorkspaceMember> InviteMember(Guid workspaceId, Guid accountId, int role)
    {
        var existing = await db.WorkspaceMembers
            .FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.AccountId == accountId);

        if (existing != null)
        {
            if (existing.LeaveAt == null)
                throw new InvalidOperationException("User is already a member.");

            existing.LeaveAt = null;
            existing.JoinedAt = null;
            existing.Role = role;
            db.WorkspaceMembers.Update(existing);
            await db.SaveChangesAsync();
            return existing;
        }

        var member = new WtWorkspaceMember
        {
            WorkspaceId = workspaceId,
            AccountId = accountId,
            Role = role
        };
        db.WorkspaceMembers.Add(member);
        await db.SaveChangesAsync();
        return member;
    }

    public async Task<WtWorkspaceMember> JoinWorkspace(Guid workspaceId, Guid accountId)
    {
        var member = await db.WorkspaceMembers
            .FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.AccountId == accountId);

        if (member == null)
            throw new InvalidOperationException("No invitation found.");

        if (member.JoinedAt != null)
            throw new InvalidOperationException("Already a member.");

        member.JoinedAt = SystemClock.Instance.GetCurrentInstant();
        db.WorkspaceMembers.Update(member);
        await db.SaveChangesAsync();
        await InvalidateUserCache(accountId);
        return member;
    }

    public async Task<WtWorkspaceMember> UpdateMemberRole(Guid workspaceId, Guid accountId, int role)
    {
        var member = await GetMember(workspaceId, accountId)
            ?? throw new InvalidOperationException("Member not found.");

        member.Role = role;
        db.WorkspaceMembers.Update(member);
        await db.SaveChangesAsync();
        return member;
    }

    public async Task RemoveMember(Guid workspaceId, Guid accountId)
    {
        var member = await GetMember(workspaceId, accountId)
            ?? throw new InvalidOperationException("Member not found.");

        member.LeaveAt = SystemClock.Instance.GetCurrentInstant();
        db.WorkspaceMembers.Update(member);
        await db.SaveChangesAsync();
        await InvalidateUserCache(accountId);
    }

    public async Task<bool> IsMemberWithRole(Guid workspaceId, Guid accountId, params int[] requiredRoles)
    {
        if (requiredRoles.Length == 0)
            return false;

        var maxRequiredRole = requiredRoles.Max();
        var member = await GetMember(workspaceId, accountId);
        return member?.Role >= maxRequiredRole;
    }

    private async Task AddDefaultRolePermissions(Guid workspaceId)
    {
        var permissions = new List<WtWorkspaceRolePermission>
        {
            new()
            {
                WorkspaceId = workspaceId,
                RoleLevel = WorkspaceMemberRole.Owner,
                CanManageWorkspace = true,
                CanManageMembers = true,
                CanManageBilling = true,
                CanCreateProjects = true,
                CanManageProjects = true,
                CanUseIdeask = true,
                CanUseDrive = true
            },
            new()
            {
                WorkspaceId = workspaceId,
                RoleLevel = WorkspaceMemberRole.Admin,
                CanManageWorkspace = false,
                CanManageMembers = true,
                CanManageBilling = false,
                CanCreateProjects = true,
                CanManageProjects = true,
                CanUseIdeask = true,
                CanUseDrive = true
            },
            new()
            {
                WorkspaceId = workspaceId,
                RoleLevel = WorkspaceMemberRole.Member,
                CanManageWorkspace = false,
                CanManageMembers = false,
                CanManageBilling = false,
                CanCreateProjects = true,
                CanManageProjects = false,
                CanUseIdeask = true,
                CanUseDrive = true
            },
            new()
            {
                WorkspaceId = workspaceId,
                RoleLevel = WorkspaceMemberRole.Viewer,
                CanManageWorkspace = false,
                CanManageMembers = false,
                CanManageBilling = false,
                CanCreateProjects = false,
                CanManageProjects = false,
                CanUseIdeask = true,
                CanUseDrive = true
            }
        };

        db.WorkspaceRolePermissions.AddRange(permissions);
    }

    private async Task InvalidateUserCache(Guid accountId)
    {
        var cacheKey = $"{CacheKeyPrefix}user:{accountId}";
        await cache.RemoveAsync(cacheKey);
    }
}
