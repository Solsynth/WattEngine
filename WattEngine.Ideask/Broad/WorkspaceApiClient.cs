using System.Text.Json;
using DysonNetwork.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace WattEngine.Ideask.Broad;

public class WorkspaceApiClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor
)
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
        var response = await client.GetAsync($"/api/workspaces/{workspaceId}");

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

    public async Task<List<string>> GetActiveMemberAccountIds(Guid workspaceId)
    {
        var client = httpClientFactory.CreateClient("valve");
        var authorization = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization))
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authorization);

        var response = await client.GetAsync($"/api/workspaces/{workspaceId}/members");
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Unable to load workspace members.");

        var json = JsonSerializer.Deserialize<JsonElement>(
            await response.Content.ReadAsStringAsync(), JsonOptions
        );

        return json.EnumerateArray()
            .Select(member => member.GetProperty("account_id").GetString())
            .Where(accountId => Guid.TryParse(accountId, out _))
            .Select(accountId => accountId!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
