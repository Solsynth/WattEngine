using System.Text.Json;
using DysonNetwork.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace WattEngine.Ideask.Broad;

public class WorkspaceApiClient(IHttpClientFactory httpClientFactory)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task<(Guid Id, WorkspacePlan Plan)> GetWorkspaceBySlug(string slug)
    {
        var client = httpClientFactory.CreateClient("valve");
        var response = await client.GetAsync($"/api/workspaces/{slug}");

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Workspace not found.");

        var json = JsonSerializer.Deserialize<JsonElement>(
            await response.Content.ReadAsStringAsync(), JsonOptions
        );

        var id = Guid.Parse(json.GetProperty("id").GetString()!);
        var plan = (WorkspacePlan)json.GetProperty("plan").GetInt32();
        return (id, plan);
    }

    public async Task<WorkspacePlan> GetWorkspacePlan(Guid workspaceId)
    {
        var client = httpClientFactory.CreateClient("valve");
        var response = await client.GetAsync($"/api/workspaces/by-id/{workspaceId}");

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Workspace not found.");

        var json = JsonSerializer.Deserialize<JsonElement>(
            await response.Content.ReadAsStringAsync(), JsonOptions
        );

        return (WorkspacePlan)json.GetProperty("plan").GetInt32();
    }

    public async Task<Dictionary<Guid, WorkspacePlan>> GetWorkspacePlans(IEnumerable<Guid> workspaceIds)
    {
        var result = new Dictionary<Guid, WorkspacePlan>();
        foreach (var id in workspaceIds)
        {
            try
            {
                result[id] = await GetWorkspacePlan(id);
            }
            catch
            {
                // skip missing workspaces
            }
        }
        return result;
    }
}
