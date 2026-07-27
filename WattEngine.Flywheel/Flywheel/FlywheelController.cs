using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.RegularExpressions;
using DysonNetwork.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WattEngine.Flywheel.Flywheel;

[ApiController]
[Authorize]
[Route("/api/workspaces/{workspaceId:guid}/apps/{appId}")]
public partial class FlywheelController(AppDatabase db, FlywheelService flywheel, IConfiguration configuration) : ControllerBase
{
    private const int DefaultPullLimit = 100;

    public class RegisterDeviceRequest
    {
        [Required, MaxLength(512)] public string DeviceId { get; set; } = string.Empty;
        [MaxLength(1024)] public string? Label { get; set; }
    }

    public class UploadOperationRequest
    {
        [Required] public Guid OperationId { get; set; }
        [Range(1, int.MaxValue)] public int SchemeVersion { get; set; }
        [Required] public byte[] Ciphertext { get; set; } = [];
    }

    public class UploadRequest
    {
        [Required, MaxLength(512)] public string DeviceId { get; set; } = string.Empty;
        [Required, MinLength(1)] public List<UploadOperationRequest> Operations { get; set; } = [];
    }

    public class AcknowledgeRequest
    {
        [Required, MaxLength(512)] public string DeviceId { get; set; } = string.Empty;
        [Range(0, long.MaxValue)] public long Cursor { get; set; }
    }

    public class CompleteRotationRequest
    {
        [Range(1, long.MaxValue)] public long MlsEpoch { get; set; }
    }

    [HttpPost("bootstrap")]
    public async Task<ActionResult<FlywheelStreamResponse>> Bootstrap(Guid workspaceId, string appId, CancellationToken ct)
    {
        var stream = await StreamAsync(workspaceId, appId, FlywheelService.ViewerRole, ct);
        return ToResponse(stream);
    }

    [HttpGet("status")]
    public async Task<ActionResult<FlywheelStreamResponse>> Status(Guid workspaceId, string appId, CancellationToken ct)
    {
        var stream = await StreamAsync(workspaceId, appId, FlywheelService.ViewerRole, ct);
        return ToResponse(stream);
    }

    [HttpPost("devices")]
    public async Task<ActionResult<FlywheelDeviceResponse>> RegisterDevice(Guid workspaceId, string appId, RegisterDeviceRequest request, CancellationToken ct)
    {
        var stream = await StreamAsync(workspaceId, appId, FlywheelService.MemberRole, ct);
        var device = await flywheel.RegisterDeviceAsync(stream, CurrentUserId(), request.DeviceId, request.Label, ct);
        return Ok(ToResponse(device));
    }

    [HttpGet("devices")]
    public async Task<ActionResult<List<FlywheelDeviceResponse>>> ListDevices(Guid workspaceId, string appId, CancellationToken ct)
    {
        var stream = await StreamAsync(workspaceId, appId, FlywheelService.ViewerRole, ct);
        var devices = await db.Devices.Where(x => x.StreamId == stream.Id && x.AccountId == CurrentUserId())
            .OrderBy(x => x.CreatedAt).ToListAsync(ct);
        return devices.Select(ToResponse).ToList();
    }

    [HttpDelete("devices/{deviceId}")]
    public async Task<IActionResult> RevokeDevice(Guid workspaceId, string appId, string deviceId, CancellationToken ct)
    {
        var stream = await StreamAsync(workspaceId, appId, FlywheelService.MemberRole, ct);
        var device = await db.Devices.SingleOrDefaultAsync(x => x.StreamId == stream.Id && x.DeviceId == deviceId, ct);
        if (device is null) return NotFound();
        if (device.AccountId != CurrentUserId()) return Forbid();
        device.IsRevoked = true;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("operations")]
    public async Task<ActionResult<List<FlywheelOperationResponse>>> Upload(Guid workspaceId, string appId, UploadRequest request, CancellationToken ct)
    {
        var stream = await StreamAsync(workspaceId, appId, FlywheelService.MemberRole, ct);
        var maxBytes = configuration.GetValue<int?>("Flywheel:MaxOperationBytes") ?? 1_048_576;
        if (request.Operations.Any(x => x.Ciphertext.Length == 0 || x.Ciphertext.Length > maxBytes))
            return BadRequest($"Each ciphertext must be between 1 and {maxBytes} bytes.");
        var accepted = await flywheel.UploadAsync(stream, CurrentUserId(), request.DeviceId,
            request.Operations.Select(x => (x.OperationId, x.SchemeVersion, x.Ciphertext)).ToList(), ct);
        var devices = await db.Devices.Where(x => x.StreamId == stream.Id).ToDictionaryAsync(x => x.Id, x => x.DeviceId, ct);
        return accepted.Select(x => ToResponse(x, devices[x.DeviceRegistrationId])).ToList();
    }

    [HttpGet("operations")]
    public async Task<ActionResult<List<FlywheelOperationResponse>>> Pull(Guid workspaceId, string appId, [FromQuery] long after = 0, [FromQuery] int limit = DefaultPullLimit, CancellationToken ct = default)
    {
        if (after < 0) return BadRequest("after must not be negative.");
        var stream = await StreamAsync(workspaceId, appId, FlywheelService.ViewerRole, ct);
        limit = Math.Clamp(limit, 1, configuration.GetValue<int?>("Flywheel:MaxPullLimit") ?? 500);
        var operations = await db.Operations.Where(x => x.StreamId == stream.Id && x.Cursor > after)
            .OrderBy(x => x.Cursor).Take(limit).ToListAsync(ct);
        var deviceIds = await db.Devices.Where(x => x.StreamId == stream.Id).ToDictionaryAsync(x => x.Id, x => x.DeviceId, ct);
        return operations.Select(x => ToResponse(x, deviceIds[x.DeviceRegistrationId])).ToList();
    }

    [HttpPost("acknowledgements")]
    public async Task<IActionResult> Acknowledge(Guid workspaceId, string appId, AcknowledgeRequest request, CancellationToken ct)
    {
        var stream = await StreamAsync(workspaceId, appId, FlywheelService.ViewerRole, ct);
        if (request.Cursor > stream.CurrentCursor) return BadRequest("cursor exceeds the stream cursor.");
        var device = await db.Devices.SingleOrDefaultAsync(x => x.StreamId == stream.Id && x.DeviceId == request.DeviceId, ct);
        if (device is null) return NotFound();
        if (device.AccountId != CurrentUserId() || device.IsRevoked) return Forbid();
        device.LastAcknowledgedCursor = Math.Max(device.LastAcknowledgedCursor, request.Cursor);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("mls/rotation-complete")]
    public async Task<IActionResult> CompleteRotation(Guid workspaceId, string appId, CompleteRotationRequest request, CancellationToken ct)
    {
        var stream = await StreamAsync(workspaceId, appId, FlywheelService.MemberRole, ct);
        await flywheel.CompleteRotationAsync(stream, request.MlsEpoch, ct);
        return NoContent();
    }

    [HttpGet("events")]
    public async Task Events(Guid workspaceId, string appId, [FromQuery] long after = 0, CancellationToken ct = default)
    {
        if (after < 0) { Response.StatusCode = StatusCodes.Status400BadRequest; return; }
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        var cursor = after;
        while (!ct.IsCancellationRequested)
        {
            var stream = await StreamAsync(workspaceId, appId, FlywheelService.ViewerRole, ct);
            if (stream.CurrentCursor > cursor)
            {
                cursor = stream.CurrentCursor;
                await Response.WriteAsync($"id: {cursor}\nevent: changes-available\ndata: {{\"cursor\":\"{cursor}\"}}\n\n", ct);
            }
            else
            {
                await Response.WriteAsync(": keep-alive\n\n", ct);
            }
            await Response.Body.FlushAsync(ct);
            await Task.Delay(TimeSpan.FromSeconds(15), ct);
        }
    }

    private async Task<FlywheelStream> StreamAsync(Guid workspaceId, string appId, int role, CancellationToken ct)
    {
        if (!AppIdRegex().IsMatch(appId)) throw new FlywheelValidationException("app_id must be a reverse-DNS package identifier.");
        return await flywheel.GetStreamAsync(workspaceId, appId, CurrentUserId(), role, ct);
    }

    private Guid CurrentUserId() => (HttpContext.Items["CurrentUser"] as SnAccount)?.Id
        ?? throw new FlywheelForbiddenException();
    private static FlywheelStreamResponse ToResponse(FlywheelStream x) => new(x.WorkspaceId, x.AppId, x.MlsGroupId, x.CurrentCursor, x.MlsEpoch, x.RequiresMlsRotation);
    private static FlywheelDeviceResponse ToResponse(FlywheelDevice x) => new(x.Id, x.DeviceId, x.Label, x.IsRevoked, x.LastAcknowledgedCursor, x.LastSeenAt);
    private static FlywheelOperationResponse ToResponse(FlywheelOperation x, string deviceId) => new(x.OperationId, deviceId, x.SchemeVersion, x.Cursor, x.Ciphertext, x.CreatedAt);

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?(?:\\.[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)+$", RegexOptions.CultureInvariant)]
    private static partial Regex AppIdRegex();
}
