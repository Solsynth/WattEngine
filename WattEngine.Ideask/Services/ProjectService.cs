using System;
using System.Collections.Generic;
using System.Linq;
using DysonNetwork.Shared.Proto;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WattEngine.Ideask.Broad;

namespace WattEngine.Ideask.Services;

public class ProjectService(AppDatabase db, IHttpContextAccessor httpContextAccessor, ILogger<ProjectService> logger)
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
        var project = await GetProjectAsync(projectId);
        if (project == null) throw new KeyNotFoundException("Project not found");
        project.Name = name;
        await db.SaveChangesAsync();
        return project;
    }

    public async global::System.Threading.Tasks.Task DeleteProjectAsync(Guid projectId)
    {
        var project = await GetProjectAsync(projectId);
        if (project == null) throw new KeyNotFoundException("Project not found");
        db.Projects.Remove(project);
        await db.SaveChangesAsync();
    }

    public async global::System.Threading.Tasks.Task AddMemberAsync(Guid projectId, Guid memberAccountId, Permission permission)
    {
        var project = await GetProjectAsync(projectId);
        if (project == null) throw new KeyNotFoundException("Project not found");
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
    }

    public async global::System.Threading.Tasks.Task RemoveMemberAsync(Guid projectId, Guid memberAccountId)
    {
        var project = await GetProjectAsync(projectId);
        if (project == null) throw new KeyNotFoundException("Project not found");
        var member = project.Members.FirstOrDefault(m => m.AccountId == memberAccountId);
        if (member == null) throw new KeyNotFoundException("Member not found");
        db.ProjectMembers.Remove(member);
        await db.SaveChangesAsync();
    }
}