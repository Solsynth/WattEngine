using DysonNetwork.Shared.Models;
using DysonNetwork.Shared.Proto;
using Microsoft.EntityFrameworkCore;
using WattEngine.Ideask.Connectivity;
using WattEngine.Ideask.Models.WebSocket;

namespace WattEngine.Ideask.Broad;

public class BroadService(
    AppDatabase db,
    IHttpContextAccessor httpContextAccessor,
    RealtimeDeliveryService webSocketService,
    WorkspaceApiClient workspaceApi
)
{
    private Guid GetCurrentAccountId()
    {
        return httpContextAccessor.HttpContext?.Items["CurrentUser"] is not SnAccount currentUser
            ? throw new UnauthorizedAccessException("User not authenticated")
            : currentUser.Id;
    }

    public async Task<WtBroad> CreateBroadAsync(string name, Guid? workspaceId = null,
        string? description = null, string? content = null, string? backgroundImageId = null,
        string? iconImageId = null, Visibility? visibility = null)
    {
        var accountId = GetCurrentAccountId();

        // Check broad quota if this broad belongs to a workspace
        if (workspaceId.HasValue)
        {
            await CheckBroadQuota(workspaceId.Value);
        }

        var broad = new WtBroad
        {
            Name = name,
            AccountId = accountId,
            WorkspaceId = workspaceId,
            Description = description,
            Content = content,
            Visibility = visibility ?? Visibility.Private
        };
        db.Broads.Add(broad);
        await db.SaveChangesAsync();

        // Send WebSocket notification
        var packet = webSocketService.CreateBroadCreatedPacket(broad, accountId);
        await webSocketService.SendToUsersAsync(new List<string> { accountId.ToString() }, packet);

        return broad;
    }

    private async System.Threading.Tasks.Task CheckBroadQuota(Guid workspaceId)
    {
        var plan = await workspaceApi.GetWorkspacePlan(workspaceId);
        var maxBroads = WorkspacePlanQuota.GetMaxBroadsPerProject(plan);

        var broadCount = await db.Broads
            .CountAsync(b => b.WorkspaceId == workspaceId && b.DeletedAt == null);

        if (broadCount >= maxBroads)
            throw new InvalidOperationException(
                $"Workspace plan ({plan}) allows max {maxBroads} broads. Current count: {broadCount}."
            );
    }

    public async Task<List<WtBroad>> GetBroadsAsync()
    {
        var accountId = GetCurrentAccountId();
        return await db.Broads
            .Where(b => b.AccountId == accountId)
            .ToListAsync();
    }

    public async Task<WtBroad?> GetBroadAsync(Guid broadId)
    {
        var accountId = GetCurrentAccountId();
        return await db.Broads
            .FirstOrDefaultAsync(b => b.Id == broadId && b.AccountId == accountId);
    }

    public async Task<WtBroad> UpdateBroadAsync(Guid broadId, string name,
        Guid? workspaceId, string? description = null, string? content = null, string? backgroundImageId = null,
        string? iconImageId = null, Visibility? visibility = null)
    {
        var accountId = GetCurrentAccountId();
        var broad = await db.Broads.FirstOrDefaultAsync(b => b.Id == broadId);

        if (broad == null) throw new KeyNotFoundException("Broad not found");

        if (broad.AccountId != accountId)
            throw new UnauthorizedAccessException("No access to broad");

        var changedProperties = new List<string>();

        if (broad.Name != name)
        {
            broad.Name = name;
            changedProperties.Add("name");
        }

        if (broad.WorkspaceId != workspaceId)
        {
            broad.WorkspaceId = workspaceId;
            changedProperties.Add("workspace_id");
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

        if (changedProperties.Any())
        {
            await db.SaveChangesAsync();

            var packet = webSocketService.CreateBroadUpdatedPacket(broad, changedProperties, accountId);
            await webSocketService.SendToUsersAsync(new List<string> { accountId.ToString() }, packet);
        }

        return broad;
    }

    public async System.Threading.Tasks.Task DeleteBroadAsync(Guid broadId)
    {
        var accountId = GetCurrentAccountId();
        var broad = await db.Broads.FirstOrDefaultAsync(b => b.Id == broadId);

        if (broad == null) throw new KeyNotFoundException("Broad not found");

        if (broad.AccountId != accountId)
            throw new UnauthorizedAccessException("No access to broad");

        db.Broads.Remove(broad);
        await db.SaveChangesAsync();

        var packet = webSocketService.CreateBroadUpdatedPacket(
            broad,
            new List<string> { "deleted" },
            accountId
        );
        await webSocketService.SendToUsersAsync(new List<string> { accountId.ToString() }, packet);
    }
}
