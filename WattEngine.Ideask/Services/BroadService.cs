using System;
using System.Collections.Generic;
using System.Linq;
using DysonNetwork.Shared.Proto;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WattEngine.Ideask.Broad;

namespace WattEngine.Ideask.Services;

public class BroadService(AppDatabase db, IHttpContextAccessor httpContextAccessor, ILogger<BroadService> logger)
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
        var broad = await GetBroadAsync(broadId);
        if (broad == null) throw new KeyNotFoundException("Broad not found");
        broad.Name = name;
        broad.ProjectId = projectId;
        await db.SaveChangesAsync();
        return broad;
    }

    public async global::System.Threading.Tasks.Task DeleteBroadAsync(Guid broadId)
    {
        var broad = await GetBroadAsync(broadId);
        if (broad == null) throw new KeyNotFoundException("Broad not found");
        db.Broads.Remove(broad);
        await db.SaveChangesAsync();
    }
}