using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NodaTime;

namespace WattEngine.Ideask.GitHub;

public sealed record GitHubRepository(long Id, string FullName, string Owner, string Name, string HtmlUrl);
public sealed record GitHubIssue(long Id, int Number, string Title, string? Body, string State, string HtmlUrl,
    Instant? UpdatedAt, bool IsPullRequest, List<string> Labels);
public sealed record GitHubComment(long Id, string Body, string Login, string? AvatarUrl, Instant? UpdatedAt);

public class GitHubApiClient(IHttpClientFactory httpClientFactory, IConfiguration configuration, IClock clock)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private HttpClient Client(string token)
    {
        var client = httpClientFactory.CreateClient("github");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("WattEngine-Ideask");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    public async System.Threading.Tasks.Task<string> GetInstallationTokenAsync(long installationId, CancellationToken ct = default)
    {
        var appId = configuration["GitHub:AppId"] ?? throw new InvalidOperationException("GitHub:AppId must be configured.");
        var privateKey = configuration["GitHub:PrivateKey"];
        var privateKeyPath = configuration["GitHub:PrivateKeyPath"];
        if (string.IsNullOrWhiteSpace(privateKey) && !string.IsNullOrWhiteSpace(privateKeyPath))
            privateKey = await File.ReadAllTextAsync(privateKeyPath, ct);
        if (string.IsNullOrWhiteSpace(privateKey)) throw new InvalidOperationException("GitHub App private key must be configured.");
        var now = clock.GetCurrentInstant();
        var header = Base64Url("{\"alg\":\"RS256\",\"typ\":\"JWT\"}");
        var payload = Base64Url(JsonSerializer.Serialize(new { iat = now.ToUnixTimeSeconds() - 30, exp = now.ToUnixTimeSeconds() + 540, iss = appId }));
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKey);
        var signature = Base64Url(rsa.SignData(Encoding.ASCII.GetBytes($"{header}.{payload}"), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        var request = new HttpRequestMessage(HttpMethod.Post, $"app/installations/{installationId}/access_tokens");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", $"{header}.{payload}.{signature}");
        request.Headers.UserAgent.ParseAdd("WattEngine-Ideask");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        var response = await httpClientFactory.CreateClient("github").SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var value = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
        return value.GetProperty("token").GetString()!;
    }

    public async System.Threading.Tasks.Task<List<GitHubRepository>> ListInstallationRepositoriesAsync(string token, CancellationToken ct = default)
    {
        var client = Client(token);
        var result = new List<GitHubRepository>();
        for (var page = 1; page <= 20; page++)
        {
            var response = await client.GetAsync($"installation/repositories?per_page=100&page={page}", ct);
            response.EnsureSuccessStatusCode();
            var pageResult = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
            var items = pageResult.GetProperty("repositories").Deserialize<List<JsonElement>>(JsonOptions) ?? [];
            result.AddRange(items.Select(ParseRepository));
            if (items.Count < 100) break;
        }
        return result;
    }

    public async System.Threading.Tasks.Task<GitHubRepository> GetRepositoryAsync(string token, string owner, string repository, CancellationToken ct = default)
    {
        var response = await Client(token).GetAsync($"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repository)}", ct);
        response.EnsureSuccessStatusCode();
        return ParseRepository(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct));
    }


    public async System.Threading.Tasks.Task<List<GitHubIssue>> ListIssuesAsync(string token, string owner, string repository, CancellationToken ct = default)
    {
        var client = Client(token);
        var result = new List<GitHubIssue>();
        for (var page = 1; page <= 50; page++)
        {
            var response = await client.GetAsync($"repos/{owner}/{repository}/issues?state=all&per_page=100&page={page}", ct);
            response.EnsureSuccessStatusCode();
            var items = await response.Content.ReadFromJsonAsync<List<JsonElement>>(JsonOptions, ct) ?? [];
            result.AddRange(items.Select(ParseIssue));
            if (items.Count < 100) break;
        }
        return result;
    }

    public async System.Threading.Tasks.Task<GitHubIssue> CreateIssueAsync(string token, string owner, string repository, string title, string? body, List<string> labels, CancellationToken ct = default)
    {
        var response = await Client(token).PostAsJsonAsync($"repos/{owner}/{repository}/issues", new { title, body, labels }, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        return ParseIssue(await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct));
    }

    public async System.Threading.Tasks.Task UpdateIssueAsync(string token, WtGitHubIntegration integration, int number, string title, string? body, List<string> labels, bool closed, CancellationToken ct = default)
    {
        var response = await Client(token).PatchAsJsonAsync($"repos/{integration.Owner}/{integration.Repository}/issues/{number}", new { title, body, labels, state = closed ? "closed" : "open" }, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
    }

    public async System.Threading.Tasks.Task<List<GitHubComment>> ListCommentsAsync(string token, WtGitHubIntegration integration, int issueNumber, CancellationToken ct = default)
    {
        var response = await Client(token).GetAsync($"repos/{integration.Owner}/{integration.Repository}/issues/{issueNumber}/comments?per_page=100", ct);
        response.EnsureSuccessStatusCode();
        var items = await response.Content.ReadFromJsonAsync<List<JsonElement>>(JsonOptions, ct) ?? [];
        return items.Select(ParseComment).ToList();
    }

    public async System.Threading.Tasks.Task<long> CreateCommentAsync(string token, WtGitHubIntegration integration, int issueNumber, string body, CancellationToken ct = default)
    {
        var response = await Client(token).PostAsJsonAsync($"repos/{integration.Owner}/{integration.Repository}/issues/{issueNumber}/comments", new { body }, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct)).GetProperty("id").GetInt64();
    }

    public async System.Threading.Tasks.Task UpdateCommentAsync(string token, WtGitHubIntegration integration, long commentId, string body, CancellationToken ct = default)
    {
        var response = await Client(token).PatchAsJsonAsync($"repos/{integration.Owner}/{integration.Repository}/issues/comments/{commentId}", new { body }, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
    }

    public async System.Threading.Tasks.Task DeleteCommentAsync(string token, WtGitHubIntegration integration, long commentId, CancellationToken ct = default)
    {
        var response = await Client(token).DeleteAsync($"repos/{integration.Owner}/{integration.Repository}/issues/comments/{commentId}", ct);
        response.EnsureSuccessStatusCode();
    }

    private static GitHubRepository ParseRepository(JsonElement e) => new(e.GetProperty("id").GetInt64(), e.GetProperty("full_name").GetString()!, e.GetProperty("owner").GetProperty("login").GetString()!, e.GetProperty("name").GetString()!, e.GetProperty("html_url").GetString()!);
    private static string Base64Url(string value) => Base64Url(Encoding.UTF8.GetBytes(value));
    private static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static GitHubIssue ParseIssue(JsonElement e) => new(e.GetProperty("id").GetInt64(), e.GetProperty("number").GetInt32(), e.GetProperty("title").GetString()!, e.TryGetProperty("body", out var body) && body.ValueKind != JsonValueKind.Null ? body.GetString() : null, e.GetProperty("state").GetString()!, e.GetProperty("html_url").GetString()!, e.TryGetProperty("updated_at", out var updated) && updated.ValueKind == JsonValueKind.String ? Instant.FromDateTimeUtc(updated.GetDateTime().ToUniversalTime()) : null, e.TryGetProperty("pull_request", out _), e.TryGetProperty("labels", out var labels) ? labels.EnumerateArray().Select(x => x.GetProperty("name").GetString()!).ToList() : []);
    private static GitHubComment ParseComment(JsonElement e) => new(e.GetProperty("id").GetInt64(), e.GetProperty("body").GetString()!, e.GetProperty("user").GetProperty("login").GetString()!, e.GetProperty("user").TryGetProperty("avatar_url", out var avatar) ? avatar.GetString() : null, e.TryGetProperty("updated_at", out var updated) ? Instant.FromDateTimeUtc(updated.GetDateTime().ToUniversalTime()) : null);
}
