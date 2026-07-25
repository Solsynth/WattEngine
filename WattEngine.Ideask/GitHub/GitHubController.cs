using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NodaTime;

namespace WattEngine.Ideask.GitHub;

[ApiController]
[Route("/api/github")]
public class GitHubController(GitHubIntegrationService integrations) : ControllerBase
{
    public record LinkRepositoryRequest(long InstallationId, string Owner, string Repository);

    [HttpGet("broads/{broadId:guid}/install-url"), Authorize]
    public async Task<IActionResult> InstallUrl(Guid broadId)
    {
        try { return Ok(new { url = await integrations.CreateInstallUrlAsync(broadId, HttpContext.RequestAborted) }); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("broads/{broadId:guid}/installations/{installationId:long}/repositories"), Authorize]
    public async Task<IActionResult> ListRepositories(Guid broadId, long installationId)
    {
        try { return Ok(await integrations.ListRepositoriesAsync(broadId, installationId, HttpContext.RequestAborted)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("broads/{broadId:guid}/installation"), Authorize]
    public async Task<IActionResult> Installation(Guid broadId)
    {
        try
        {
            var installationId = await integrations.GetCompletedInstallationAsync(broadId, HttpContext.RequestAborted);
            return installationId.HasValue ? Ok(new { installationId }) : NotFound();
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("broads/{broadId:guid}"), Authorize]
    public async Task<IActionResult> Status(Guid broadId)
    {
        try { return Ok(await integrations.GetStatusAsync(broadId)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("broads/{broadId:guid}"), Authorize]
    public async Task<IActionResult> Link(Guid broadId, [FromBody] LinkRepositoryRequest request)
    {
        try { return Ok(await integrations.LinkAsync(broadId, request.InstallationId, request.Owner, request.Repository, HttpContext.RequestAborted)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("broads/{broadId:guid}/sync"), Authorize]
    public async Task<IActionResult> Sync(Guid broadId)
    {
        try { await integrations.ReconcileAsync(broadId, HttpContext.RequestAborted); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }

    [HttpDelete("broads/{broadId:guid}"), Authorize]
    public async Task<IActionResult> Unlink(Guid broadId)
    {
        try { await integrations.UnlinkAsync(broadId, HttpContext.RequestAborted); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("webhook"), AllowAnonymous]
    public async Task<IActionResult> Webhook()
    {
        using var stream = new MemoryStream();
        await Request.Body.CopyToAsync(stream, HttpContext.RequestAborted);
        var payload = stream.ToArray();
        using var document = JsonDocument.Parse(payload);
        var signature = Request.Headers["X-Hub-Signature-256"].ToString();
        if (!await integrations.VerifySignatureAsync(signature, payload)) return Unauthorized();
        var eventName = Request.Headers["X-GitHub-Event"].ToString();
        var action = document.RootElement.TryGetProperty("action", out var actionValue) ? actionValue.GetString() ?? string.Empty : string.Empty;
        if (!document.RootElement.TryGetProperty("repository", out var repository) && eventName is "issues" or "issue_comment" or "repository") return BadRequest();
        var repositoryId = repository.ValueKind == JsonValueKind.Undefined ? 0 : repository.GetProperty("id").GetInt64();
        if (eventName == "repository" && action is "deleted" or "archived") { await integrations.RemoveForRepositoryAsync(repositoryId, HttpContext.RequestAborted); return NoContent(); }
        if (eventName == "installation" && action == "deleted" && document.RootElement.TryGetProperty("installation", out var installation))
        {
            await integrations.RemoveForInstallationAsync(installation.GetProperty("id").GetInt64(), HttpContext.RequestAborted);
            return NoContent();
        }
        if (eventName == "installation_repositories" && document.RootElement.TryGetProperty("repositories_removed", out var removed))
        {
            foreach (var removedRepository in removed.EnumerateArray())
                await integrations.RemoveForRepositoryAsync(removedRepository.GetProperty("id").GetInt64(), HttpContext.RequestAborted);
            return NoContent();
        }
        var issue = document.RootElement.TryGetProperty("issue", out var issueValue) ? ParseIssue(issueValue) : null;
        var comment = document.RootElement.TryGetProperty("comment", out var commentValue) ? ParseComment(commentValue) : null;
        if (eventName is "issues" or "issue_comment") await integrations.HandleWebhookAsync(repositoryId, issue, comment, action, HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpGet("installation-complete"), AllowAnonymous]
    public async Task<IActionResult> InstallationComplete([FromQuery(Name = "installation_id")] long installationId, [FromQuery] string state)
    {
        try
        {
            await integrations.CompleteInstallationAsync(installationId, state, HttpContext.RequestAborted);
            return Content("GitHub App installation completed. You can return to WattEngine and select a repository.", "text/plain");
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    private static GitHubIssue ParseIssue(JsonElement e) => new(e.GetProperty("id").GetInt64(), e.GetProperty("number").GetInt32(), e.GetProperty("title").GetString()!, e.TryGetProperty("body", out var body) && body.ValueKind != JsonValueKind.Null ? body.GetString() : null, e.GetProperty("state").GetString()!, e.GetProperty("html_url").GetString()!, e.TryGetProperty("updated_at", out var updated) ? Instant.FromDateTimeUtc(updated.GetDateTime().ToUniversalTime()) : null, e.TryGetProperty("pull_request", out _), e.TryGetProperty("labels", out var labels) ? labels.EnumerateArray().Select(x => x.GetProperty("name").GetString()!).ToList() : []);
    private static GitHubComment ParseComment(JsonElement e) => new(e.GetProperty("id").GetInt64(), e.GetProperty("body").GetString()!, e.GetProperty("user").GetProperty("login").GetString()!, e.GetProperty("user").TryGetProperty("avatar_url", out var avatar) ? avatar.GetString() : null, e.TryGetProperty("updated_at", out var updated) ? Instant.FromDateTimeUtc(updated.GetDateTime().ToUniversalTime()) : null);
}
