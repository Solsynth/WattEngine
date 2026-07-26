using DysonNetwork.Shared.Registry;
using DysonNetwork.Shared.Proto;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace WattEngine.Flywheel.Flywheel;

public class FlywheelService(
    AppDatabase db,
    RemoteWorkspaceService workspaces,
    RemoteMlsService mls,
    IConfiguration configuration,
    IClock clock)
{
    public const int ViewerRole = 25;
    public const int MemberRole = 50;

    public static string GetMlsGroupId(Guid workspaceId, string appId) => $"flywheel:{workspaceId:D}:{appId}";

    public async Task<FlywheelStream> GetStreamAsync(
        Guid workspaceId, string appId, Guid accountId, int requiredRole, CancellationToken ct)
    {
        if (!await workspaces.IsMemberWithRole(workspaceId, accountId, [requiredRole], ct))
            throw new FlywheelForbiddenException();
        if (await workspaces.GetPlan(workspaceId, ct) is not (DyWorkspacePlan.Pro or DyWorkspacePlan.Enterprise))
            throw new FlywheelSubscriptionRequiredException();

        var stream = await db.Streams.SingleOrDefaultAsync(
            x => x.WorkspaceId == workspaceId && x.AppId == appId, ct);
        var now = clock.GetCurrentInstant();
        if (stream is null)
        {
            stream = new FlywheelStream
            {
                WorkspaceId = workspaceId,
                AppId = appId,
                MlsGroupId = GetMlsGroupId(workspaceId, appId),
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Streams.Add(stream);
            await db.SaveChangesAsync(ct);
        }

        await RefreshMembershipAsync(stream, ct);
        return stream;
    }

    public async Task<FlywheelDevice> RegisterDeviceAsync(
        FlywheelStream stream, Guid accountId, string deviceId, string? label, CancellationToken ct)
    {
        var device = await db.Devices.SingleOrDefaultAsync(
            x => x.StreamId == stream.Id && x.DeviceId == deviceId, ct);
        var now = clock.GetCurrentInstant();
        if (device is not null)
        {
            if (device.AccountId != accountId)
                throw new FlywheelConflictException("The device ID is already registered by another account.");
            if (device.IsRevoked)
                throw new FlywheelConflictException("A revoked device ID cannot be re-registered.");
            device.Label = label;
            device.LastSeenAt = now;
            device.UpdatedAt = now;
        }
        else
        {
            device = new FlywheelDevice
            {
                StreamId = stream.Id,
                AccountId = accountId,
                DeviceId = deviceId,
                Label = label,
                LastSeenAt = now,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Devices.Add(device);
        }
        await db.SaveChangesAsync(ct);
        return device;
    }

    public async Task<List<FlywheelOperation>> UploadAsync(
        FlywheelStream stream, Guid accountId, string deviceId, IReadOnlyList<(Guid OperationId, int SchemeVersion, byte[] Ciphertext)> operations,
        CancellationToken ct)
    {
        if (stream.RequiresMlsRotation)
            throw new FlywheelConflictException("An MLS epoch rotation is required before publishing new operations.");

        var device = await db.Devices.SingleOrDefaultAsync(
            x => x.StreamId == stream.Id && x.DeviceId == deviceId, ct)
            ?? throw new FlywheelNotFoundException("Device registration not found.");
        if (device.AccountId != accountId || device.IsRevoked)
            throw new FlywheelForbiddenException();

        var ids = operations.Select(x => x.OperationId).Distinct().ToList();
        if (ids.Count != operations.Count)
            throw new FlywheelConflictException("An upload batch cannot contain duplicate operation IDs.");
        var existing = await db.Operations.Where(x => x.StreamId == stream.Id && ids.Contains(x.OperationId))
            .ToDictionaryAsync(x => x.OperationId, ct);
        var accepted = new List<FlywheelOperation>();
        var now = clock.GetCurrentInstant();
        var retentionDays = Math.Max(1, configuration.GetValue<int?>("Flywheel:OperationRetentionDays") ?? 90);

        foreach (var input in operations)
        {
            if (existing.TryGetValue(input.OperationId, out var duplicate))
            {
                accepted.Add(duplicate);
                continue;
            }

            stream.CurrentCursor++;
            stream.UpdatedAt = now;
            var operation = new FlywheelOperation
            {
                StreamId = stream.Id,
                DeviceRegistrationId = device.Id,
                OperationId = input.OperationId,
                SchemeVersion = input.SchemeVersion,
                Cursor = stream.CurrentCursor,
                Ciphertext = input.Ciphertext,
                CreatedAt = now,
                RetainUntil = now + Duration.FromDays(retentionDays)
            };
            db.Operations.Add(operation);
            accepted.Add(operation);
        }

        device.LastSeenAt = now;
        device.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return accepted;
    }

    public async Task CompleteRotationAsync(FlywheelStream stream, long mlsEpoch, CancellationToken ct)
    {
        if (!stream.RequiresMlsRotation)
            return;

        var state = await mls.GetGroupStateAsync(stream.MlsGroupId);
        if (state.Epoch < mlsEpoch || mlsEpoch <= stream.MlsEpoch)
            throw new FlywheelConflictException("The requested MLS epoch has not been committed.");

        stream.MlsEpoch = mlsEpoch;
        stream.RequiresMlsRotation = false;
        stream.UpdatedAt = clock.GetCurrentInstant();
        await db.SaveChangesAsync(ct);
    }

    public async Task RefreshMembershipAsync(FlywheelStream stream, CancellationToken ct)
    {
        var active = (await workspaces.GetActiveMemberAccountIds(stream.WorkspaceId, ct))
            .Select(Guid.Parse).ToHashSet();
        var observed = await db.StreamMembers.Where(x => x.StreamId == stream.Id).ToListAsync(ct);
        var removed = observed.Any(x => !active.Contains(x.AccountId));
        if (removed)
        {
            stream.RequiresMlsRotation = true;
            stream.UpdatedAt = clock.GetCurrentInstant();
            db.StreamMembers.RemoveRange(observed.Where(x => !active.Contains(x.AccountId)));
        }

        var known = observed.Select(x => x.AccountId).ToHashSet();
        var now = clock.GetCurrentInstant();
        foreach (var accountId in active.Where(x => !known.Contains(x)))
        {
            db.StreamMembers.Add(new FlywheelStreamMember { StreamId = stream.Id, AccountId = accountId, ObservedAt = now });
        }
        if (removed || active.Any(x => !known.Contains(x)))
            await db.SaveChangesAsync(ct);
    }
}

public class FlywheelForbiddenException : Exception;
public class FlywheelNotFoundException(string message) : Exception(message);
public class FlywheelConflictException(string message) : Exception(message);
public class FlywheelValidationException(string message) : Exception(message);
public class FlywheelSubscriptionRequiredException : Exception
{
    public FlywheelSubscriptionRequiredException() : base("Flywheel requires a Pro or Enterprise workspace subscription.") { }
}
