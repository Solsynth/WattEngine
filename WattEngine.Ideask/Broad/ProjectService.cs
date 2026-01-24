using DysonNetwork.Shared.Proto;
using Microsoft.EntityFrameworkCore;
using WattEngine.Ideask.Connectivity;

namespace WattEngine.Ideask.Broad;

public class ProjectService(AppDatabase db, IHttpContextAccessor httpContextAccessor, ILogger<ProjectService> logger, RealtimeDeliveryService webSocketService)
{
    private Guid GetCurrentAccountId()
    {
        var currentUser = httpContextAccessor.HttpContext?.Items["CurrentUser"] as Account;
        if (currentUser == null) throw new UnauthorizedAccessException("User not authenticated");
        return Guid.Parse(currentUser.Id);
    }

    public async global::System.Threading.Tasks.Task<WtProject> CreateProjectAsync(string name)
    {
        var accountId = GetCurrentAccountId();
        var project = new WtProject
        {
            Name = name,
            CreatorAccountId = accountId
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        
        // Send WebSocket notification
        var packet = webSocketService.CreateProjectCreatedPacket(project, accountId);
        await webSocketService.SendToUsersAsync(new List<string> { accountId.ToString() }, packet);
        
        return project;
    }

    public async global::System.Threading.Tasks.Task<List<WtProject>> GetProjectsAsync()
    {
        var accountId = GetCurrentAccountId();
        return await db.Projects
            .Where(p => p.CreatorAccountId == accountId || p.Members.Any(m => m.AccountId == accountId))
            .ToListAsync();
    }

    public async global::System.Threading.Tasks.Task<WtProject?> GetProjectAsync(Guid projectId)
    {
        var accountId = GetCurrentAccountId();
        return await db.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == projectId && (p.CreatorAccountId == accountId || p.Members.Any(m => m.AccountId == accountId)));
    }

    public async global::System.Threading.Tasks.Task<WtProject> UpdateProjectAsync(Guid projectId, string name)
    {
        var accountId = GetCurrentAccountId();
        var project = await db.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == projectId);
        
        if (project == null) throw new KeyNotFoundException("Project not found");
        
        // Check access
        if (project.CreatorAccountId != accountId && !project.Members.Any(m => m.AccountId == accountId))
            throw new UnauthorizedAccessException("No access to project");

        var changedProperties = new List<string>();
        if (project.Name != name)
        {
            project.Name = name;
            changedProperties.Add("name");
        }

        if (changedProperties.Any())
        {
            await db.SaveChangesAsync();
            
            var packet = webSocketService.CreateProjectUpdatedPacket(project, changedProperties, accountId);
            var userIds = project.Members.Select(m => m.AccountId.ToString()).ToList();
            userIds.Add(project.CreatorAccountId.ToString());
            
            await webSocketService.SendToUsersAsync(userIds.Distinct().ToList(), packet);
        }

        return project;
    }

    public async global::System.Threading.Tasks.Task DeleteProjectAsync(Guid projectId)
    {
        var accountId = GetCurrentAccountId();
        var project = await db.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == projectId);
        
        if (project == null) throw new KeyNotFoundException("Project not found");
        
        // Check access - only creator can delete
        if (project.CreatorAccountId != accountId)
            throw new UnauthorizedAccessException("Only project creator can delete project");

        db.Projects.Remove(project);
        await db.SaveChangesAsync();
        
        var packet = webSocketService.CreateProjectUpdatedPacket(
            project, 
            new List<string> { "deleted" }, 
            accountId
        );
        var userIds = project.Members.Select(m => m.AccountId.ToString()).ToList();
        userIds.Add(project.CreatorAccountId.ToString());
        
        await webSocketService.SendToUsersAsync(userIds.Distinct().ToList(), packet);
    }

    public async global::System.Threading.Tasks.Task AddMemberAsync(Guid projectId, Guid memberAccountId, Permission permission)
    {
        var accountId = GetCurrentAccountId();
        var project = await db.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == projectId);
        
        if (project == null) throw new KeyNotFoundException("Project not found");
        
        // Check access - only creator can add members
        if (project.CreatorAccountId != accountId)
            throw new UnauthorizedAccessException("Only project creator can add members");
            
        if (project.Members.Any(m => m.AccountId == memberAccountId)) throw new InvalidOperationException("Member already exists");
        
        var member = new WtProjectMember
        {
            ProjectId = projectId,
            AccountId = memberAccountId,
            Permission = permission,
            IsCreator = false
        };
        db.ProjectMembers.Add(member);
        await db.SaveChangesAsync();
        
        var packet = webSocketService.CreateProjectMemberChangedPacket(
            project, 
            new List<string> { memberAccountId.ToString() }, 
            new List<string>(),
            accountId
        );
        var userIds = project.Members.Select(m => m.AccountId.ToString()).ToList();
        userIds.Add(project.CreatorAccountId.ToString());
        userIds.Add(memberAccountId.ToString()); // Also notify the new member
        
        await webSocketService.SendToUsersAsync(userIds.Distinct().ToList(), packet);
    }

    public async global::System.Threading.Tasks.Task RemoveMemberAsync(Guid projectId, Guid memberAccountId)
    {
        var accountId = GetCurrentAccountId();
        var project = await db.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == projectId);
        
        if (project == null) throw new KeyNotFoundException("Project not found");
        
        // Check access - only creator can remove members
        if (project.CreatorAccountId != accountId)
            throw new UnauthorizedAccessException("Only project creator can remove members");
            
        var member = project.Members.FirstOrDefault(m => m.AccountId == memberAccountId);
        if (member == null) throw new KeyNotFoundException("Member not found");
        
        db.ProjectMembers.Remove(member);
        await db.SaveChangesAsync();
        
        var packet = webSocketService.CreateProjectMemberChangedPacket(
            project, 
            new List<string>(), 
            new List<string> { memberAccountId.ToString() },
            accountId
        );
        var userIds = project.Members.Select(m => m.AccountId.ToString()).ToList();
        userIds.Add(project.CreatorAccountId.ToString());
        userIds.Add(memberAccountId.ToString()); // Also notify the removed member
        
        await webSocketService.SendToUsersAsync(userIds.Distinct().ToList(), packet);
    }
}