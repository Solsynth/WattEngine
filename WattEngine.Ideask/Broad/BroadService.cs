using DysonNetwork.Shared.Models;
using DysonNetwork.Shared.Proto;
using Microsoft.EntityFrameworkCore;
using WattEngine.Ideask.Connectivity;
using WattEngine.Ideask.Models.WebSocket;

namespace WattEngine.Ideask.Broad;

public class BroadService(
    AppDatabase db,
    IHttpContextAccessor httpContextAccessor,
    RealtimeDeliveryService webSocketService)
{
    private Guid GetCurrentAccountId()
    {
        return httpContextAccessor.HttpContext?.Items["CurrentUser"] is not Account currentUser
            ? throw new UnauthorizedAccessException("User not authenticated")
            : Guid.Parse(currentUser.Id);
    }

    public async global::System.Threading.Tasks.Task<WtBroad> CreateBroadAsync(string name, Guid? projectId,
        string? description = null, string? content = null, string? backgroundImageId = null,
        string? iconImageId = null, Visibility? visibility = null)
    {
        var accountId = GetCurrentAccountId();

        // Convert file IDs to SnCloudFileReferenceObject
        SnCloudFileReferenceObject? backgroundImage = null;
        SnCloudFileReferenceObject? iconImage = null;

        if (!string.IsNullOrEmpty(backgroundImageId))
        {
            var file = await GetFileAsync(backgroundImageId);
            if (file != null)
                backgroundImage = file;
        }

        if (!string.IsNullOrEmpty(iconImageId))
        {
            var file = await GetFileAsync(iconImageId);
            if (file != null)
                iconImage = file;
        }

        var broad = new WtBroad
        {
            Name = name,
            AccountId = accountId,
            ProjectId = projectId,
            Description = description,
            Content = content,
            BackgroundImage = backgroundImage,
            IconImage = iconImage,
            Visibility = visibility ?? Visibility.Private
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
            .Where(b => b.AccountId == accountId || (b.Project != null && (b.Project.AccountId == accountId ||
                                                                           b.Project.Members.Any(m =>
                                                                               m.AccountId == accountId))))
            .ToListAsync();
    }

    public async Task<WtBroad?> GetBroadAsync(Guid broadId)
    {
        var accountId = GetCurrentAccountId();
        return await db.Broads
            .FirstOrDefaultAsync(b => b.Id == broadId && (b.AccountId == accountId || (b.Project != null &&
                (b.Project.AccountId == accountId || b.Project.Members.Any(m => m.AccountId == accountId)))));
    }

    public async Task<WtBroad> UpdateBroadAsync(Guid broadId, string name,
        Guid? projectId, string? description = null, string? content = null, string? backgroundImageId = null,
        string? iconImageId = null, Visibility? visibility = null)
    {
        var accountId = GetCurrentAccountId();
        var broad = await db.Broads
            .Include(b => b.Project)
            .ThenInclude(p => p.Members)
            .FirstOrDefaultAsync(b => b.Id == broadId);

        if (broad == null) throw new KeyNotFoundException("Broad not found");

        // Check access
        if (broad.AccountId != accountId && (broad.Project == null || (broad.Project.AccountId != accountId &&
                                                                       broad.Project.Members.All(m =>
                                                                           m.AccountId != accountId))))
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

        if (broad.Description != description)
        {
            broad.Description = description;
            changedProperties.Add("description");
        }

        if (broad.Content != content)
        {
            broad.Content = content;
            changedProperties.Add("content");
        }

        if (visibility.HasValue && broad.Visibility != visibility.Value)
        {
            broad.Visibility = visibility.Value;
            changedProperties.Add("visibility");
        }

        // Handle background image
        if (!string.IsNullOrEmpty(backgroundImageId))
        {
            var file = await GetFileAsync(backgroundImageId);
            if (file != null)
            {
                if (!Equals(broad.BackgroundImage, file))
                {
                    broad.BackgroundImage = file;
                    changedProperties.Add("background_image");
                }
            }
        }
        else if (backgroundImageId == "" && broad.BackgroundImage != null)
        {
            // Clear background image
            broad.BackgroundImage = null;
            changedProperties.Add("background_image");
        }

        // Handle icon image
        if (!string.IsNullOrEmpty(iconImageId))
        {
            var file = await GetFileAsync(iconImageId);
            if (file != null)
            {
                if (!Equals(broad.IconImage, file))
                {
                    broad.IconImage = file;
                    changedProperties.Add("icon_image");
                }
            }
        }
        else if (iconImageId == "" && broad.IconImage != null)
        {
            // Clear icon image
            broad.IconImage = null;
            changedProperties.Add("icon_image");
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
        if (broad.AccountId != accountId && (broad.Project == null || (broad.Project.AccountId != accountId &&
                                                                       !broad.Project.Members.Any(m =>
                                                                           m.AccountId == accountId))))
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

    private async Task<SnCloudFileReferenceObject?> GetFileAsync(string fileId)
    {
        try
        {
            // This would need to be injected as a dependency, but for now we'll assume it's available
            // In a real implementation, you'd inject the FileService client
            return null; // Placeholder - would need actual file service implementation
        }
        catch
        {
            return null;
        }
    }

    private async System.Threading.Tasks.Task SendWebSocketPacketToProjectMembersAsync(WtBroad broad,
        IdeaskWebSocketPacket packet)
    {
        var userIds = new List<string>
        {
            // Add broad creator
            broad.AccountId.ToString()
        };

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