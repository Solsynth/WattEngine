# Workspace gRPC API

`WattEngine.Valve` exposes the shared `proto.DyWorkspaceService` gRPC service. The generated C# client and message types are in `DysonNetwork.Shared.Proto`.

## Client setup

Inter-service callers **must use gRPC**, never Valve HTTP (`/api/workspaces`).

Register with the shared helper (Blade discovery → `https://_grpc.valve`):

```csharp
// Program.cs
builder.Services.AddWorkspaceService(); // registers DyWorkspaceServiceClient + RemoteWorkspaceService
```

Prefer `RemoteWorkspaceService` (same pattern as `RemoteRealmService` / `RemoteRingService`):

```csharp
using DysonNetwork.Shared.Registry;

public class Example(RemoteWorkspaceService workspaces)
{
    public async Task Demo(Guid accountId)
    {
        var workspace = await workspaces.GetWorkspaceBySlug("my-workspace");
        var individualWorkspace = await workspaces.GetIndividualWorkspace(accountId);
        var plan = await workspaces.GetPlan(Guid.Parse(workspace.Id));
    }
}
```

Or inject `DyWorkspaceService.DyWorkspaceServiceClient` directly. All IDs are GUID strings.

## Methods

### GetWorkspace

`DyGetWorkspaceRequest` has a `oneof` query: set exactly one of `id` or `slug`.

| Request field | Type | Description |
|---|---|---|
| `id` | `string` | Workspace GUID |
| `slug` | `string` | Workspace slug |

Returns `DyWorkspace`. Invalid or missing queries return `InvalidArgument`; a missing workspace returns `NotFound`.

### GetWorkspaceBatch

Accepts `DyGetWorkspaceBatchRequest.ids` (`repeated string`) and returns `DyGetWorkspaceBatchResponse.workspaces` (`repeated DyWorkspace`). Duplicate IDs are evaluated once; missing workspaces are omitted. Invalid IDs return `InvalidArgument`.

### GetUserWorkspaces

Accepts `DyGetUserWorkspacesRequest.account_id` and returns `DyGetUserWorkspacesResponse.workspace_ids`. Only active workspace memberships are included.

### GetIndividualWorkspace

Accepts `DyGetUserWorkspacesRequest.account_id` and returns the `DyWorkspace` that is the account's active individual workspace. This is the account-owned workspace created automatically when Valve processes the account-created event; it is not a membership lookup.

`GetIndividualWorkspace` returns `NotFound` if provisioning has not completed (or the workspace was deleted) and `InvalidArgument` for an invalid account ID. Use `RemoteWorkspaceService.GetIndividualWorkspace(accountId)` for inter-service callers.

### IsMemberWithRole

Accepts `workspace_id`, `account_id`, and `required_roles` (`repeated int`). It returns `google.protobuf.BoolValue`.

The account must be an active member whose role is at least the highest requested role. An empty `required_roles` list returns `false`.

| Role | Value |
|---|---:|
| Viewer | 25 |
| Member | 50 |
| Admin | 75 |
| Owner | 100 |

### HasPermission

Accepts `workspace_id`, `account_id`, and a permission key. Returns `google.protobuf.BoolValue` indicating the effective workspace permission.

### GetPlanQuota

Accepts `DyGetPlanQuotaRequest.plan` (`DyWorkspacePlan`) and returns `DyWorkspacePlanQuota` with the limits for projects, members, tasks, boards, and storage.

### LoadMemberAccount / LoadMemberAccounts

These methods refresh supplied member references from active workspace membership records.

- `LoadMemberAccount` accepts `DyLoadWorkspaceMemberRequest.member` and returns `DyWorkspaceMember`.
- `LoadMemberAccounts` accepts `DyLoadWorkspaceMembersRequest.members` and returns `DyLoadWorkspaceMembersResponse.members`.

Each supplied member must contain `workspace_id` and `account_id`. If no active membership is found, the supplied member reference is returned unchanged.

## Response messages

`DyWorkspace` includes workspace identity, name, description, type, owner, plan, optional expiration, picture, and background. `DyWorkspaceMember` includes membership identity, workspace/account IDs, role, and optional join/leave timestamps.

## Error behavior

Malformed GUID fields return gRPC `InvalidArgument`. `GetWorkspace` and `GetIndividualWorkspace` return `NotFound` when the requested workspace does not exist. Callers should propagate gRPC cancellation/deadline tokens as appropriate.
