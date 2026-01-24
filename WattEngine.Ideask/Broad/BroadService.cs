using DysonNetwork.Shared.Proto;
using Microsoft.EntityFrameworkCore;
using WattEngine.Ideask.Connectivity;
using WattEngine.Ideask.Models.WebSocket;

namespace WattEngine.Ideask.Broad;

public class BroadService(AppDatabase db, IHttpContextAccessor httpContextAccessor, ILogger<BroadService> logger, RealtimeDeliveryService webSocketService)
{
    private Guid GetCurrentAccountId()
    {
        var currentUser = httpContextAccessor.HttpContext?.Items["CurrentUser"] as Account;
        if (currentUser == null) throw new UnauthorizedAccessException("User not authenticated");
        return Guid.Parse(currentUser.Id);
    }

    public async global::System.Threading.Tasks.Task<WtBroad> CreateBroadAsync(string name, Guid? projectId)
    {
        var accountId = GetCurrentAccountId();
        var broad = new WtBroad
        {
            Name = name,
            AccountId = accountId,
            ProjectId = projectId
        };
        db.Broads.Add(broad);
        await db.SaveChangesAsync();
        
        // Get project data for notification
        WtProject? project = null;
        if (projectId.HasValue)
        {
            project = await db.Projects.FindAsync(projectId.Value);
        }
        
        // Send WebSocket notification
        var packet = webSocketService.CreateBroadCreatedPacket(broad, project, accountId);
        await SendWebSocketPacketToProjectMembersAsync(broad, packet);
        
        return broad;
    }

    public async global::System.Threading.Tasks.Task<List<WtBroad>> GetBroadsAsync()
    {
        var accountId = GetCurrentAccountId();
        return await db.Broads
            .Where(b => b.AccountId == accountId || (b.Project != null && (b.Project.CreatorAccountId == accountId || b.Project.Members.Any(m => m.AccountId == accountId))))
            .ToListAsync();
    }

    public async global::System.Threading.Tasks.Task<WtBroad?> GetBroadAsync(Guid broadId)
    {
        var accountId = GetCurrentAccountId();
        return await db.Broads
            .FirstOrDefaultAsync(b => b.Id == broadId && (b.AccountId == accountId || (b.Project != null && (b.Project.CreatorAccountId == accountId || b.Project.Members.Any(m => m.AccountId == accountId)))));
    }

    public async global::System.Threading.Tasks.Task<WtBroad> UpdateBroadAsync(Guid broadId, string name, Guid? projectId)
    {
        var accountId = GetCurrentAccountId();
        var broad = await db.Broads
            .Include(b => b.Project)
            .FirstOrDefaultAsync(b => b.Id == broadId);
        
        if (broad == null) throw new KeyNotFoundException("Broad not found");
        
        // Check access
        if (broad.AccountId != accountId && (broad.Project == null || (broad.Project.CreatorAccountId != accountId && !broad.Project.Members.Any(m => m.AccountId == accountId))))
            throw new UnauthorizedAccessException("No access to broad");

        var changedProperties = new List<string>();
        if (broad.Name != name)
        {
            broad.Name = name;
            changedProperties.Add("name");
        }
        
        if (broad.ProjectId != projectId)
        {
            broad.ProjectId = projectId;
            changedProperties.Add("project_id");
        }

        if (changedProperties.Any())
        {
            await db.SaveChangesAsync();
            
            var packet = webSocketService.CreateBroadUpdatedPacket(broad, broad.Project, changedProperties, accountId);
            await SendWebSocketPacketToProjectMembersAsync(broad, packet);
        }

        return broad;
    }

    public async global::System.Threading.Tasks.Task DeleteBroadAsync(Guid broadId)
    {
        var accountId = GetCurrentAccountId();
        var broad = await db.Broads
            .Include(b => b.Project)
            .FirstOrDefaultAsync(b => b.Id == broadId);
        
        if (broad == null) throw new KeyNotFoundException("Broad not found");
        
        // Check access
        if (broad.AccountId != accountId && (broad.Project == null || (broad.Project.CreatorAccountId != accountId && !broad.Project.Members.Any(m => m.AccountId == accountId))))
            throw new UnauthorizedAccessException("No access to broad");

        db.Broads.Remove(broad);
        await db.SaveChangesAsync();
        
        var packet = webSocketService.CreateBroadUpdatedPacket(
            broad, 
            broad.Project, 
            new List<string> { "deleted" }, 
            accountId
        );
        await SendWebSocketPacketToProjectMembersAsync(broad, packet);
    }

    private async global::System.Threading.Tasks.Task SendWebSocketPacketToProjectMembersAsync(WtBroad broad, IdeaskWebSocketPacket packet)
    {
        var userIds = new List<string>();
        
        // Add broad creator
        userIds.Add(broad.AccountId.ToString());
        
        // Add project members if broad belongs to a project
        if (broad.Project != null)
        {
            var projectMembers = await db.ProjectMembers
                .Where(pm => pm.ProjectId == broad.Project.Id)
                .Select(pm => pm.AccountId.ToString())
                .ToListAsync();
            userIds.AddRange(projectMembers);
        }

        await webSocketService.SendToUsersAsync(userIds.Distinct().ToList(), packet);
    }
}