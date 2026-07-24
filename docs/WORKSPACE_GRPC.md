# Workspace gRPC API

`WattEngine.Valve` exposes the shared `proto.DyWorkspaceService` gRPC service. The generated C# client and message types are in `DysonNetwork.Shared.Proto`.

## Client setup

Register or construct `DyWorkspaceService.DyWorkspaceServiceClient` using the Valve gRPC endpoint. All IDs below are GUID strings.

```csharp
using DysonNetwork.Shared.Proto;

var workspace = await client.GetWorkspaceAsync(new DyGetWorkspaceRequest
{
    Slug = "my-workspace"
});
```

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

Malformed GUID fields return gRPC `InvalidArgument`. `GetWorkspace` returns `NotFound` when the workspace does not exist. Callers should propagate gRPC cancellation/deadline tokens as appropriate.
