# WattEngine Flywheel API

Flywheel is an opaque, end-to-end encrypted operation transport. A stream is
always scoped to a Valve workspace and an application package identifier:

```text
/api/flywheel/workspaces/{workspaceId}/apps/{appId}
```

`appId` is required and must be the reverse-DNS application identifier used by
Ring and the client WebSocket namespace, for example `dev.solsynth.maidkit`.
There is no default app identifier and no cross-package access path.

## Workspace setup

Flywheel does not create or manage its own workspaces. The app must use Valve
as the source of truth, then pass the selected workspace UUID to Flywheel.

| Need | Valve endpoint | Who may call it |
| --- | --- | --- |
| Show the workspace picker | `GET /api/workspaces` | Authenticated workspace member |
| Resolve a selected workspace | `GET /api/workspaces/{slugOrId}` | Any caller; use the returned `id` for Flywheel |
| Confirm sync eligibility | `GET /api/workspaces/{slugOrId}/plan/status` | Viewer+ |
| Show collaborators | `GET /api/workspaces/{slugOrId}/members` | Viewer+ |
| Check an app-specific permission | `GET /api/workspaces/{slug}/permissions/check?key={key}` | Workspace member |

The app should only offer sync when `plan` is `1` (Pro) or `2` (Enterprise).
Flywheel performs the same check on every request, so this client-side check is
for UX only and must not be treated as authorization.

### Recommended client flow

1. Call `GET /api/workspaces` and let the user select a workspace.
2. Call `GET /api/workspaces/{workspaceId}/plan/status`. If the plan is Free,
   show an upgrade message instead of starting sync.
3. Use the selected workspace `id` and the app's fixed package ID to call
   Flywheel `GET bootstrap`.
4. Register the authenticated account's MLS-capable device, establish or join
   the deterministic MLS group, then pull operations from cursor `0` or the
   locally saved cursor.
5. Keep the SSE connection open while sync is enabled. On every notification,
   pull from the last applied cursor; never treat the notification as data.
6. If `requires_mls_rotation` is true, stop uploads, update the MLS group
   membership/epoch, call `POST mls/rotation-complete`, then resume sync.

### Workspace access matrix

| Workspace role | Bootstrap / pull / SSE / acknowledge | Devices / upload / MLS rotation completion |
| --- | --- | --- |
| Viewer | Yes | No |
| Member | Yes | Yes, for their own devices |
| Admin | Yes | Yes, for their own devices |
| Owner | Yes | Yes, for their own devices |

Removing a member in Valve eventually triggers the Flywheel rotation gate. A
removed member loses access immediately on their next request; no MLS rotation
can revoke data they had already decrypted locally.

## Endpoints

- `GET bootstrap` and `GET status` return the current cursor, MLS group ID,
  MLS epoch, and whether a membership removal requires a key rotation.
- `POST devices`, `GET devices`, and `DELETE devices/{deviceId}` manage the
  authenticated user's device registrations.
- `POST operations` accepts a device ID and opaque encrypted operations.
  `operation_id` is idempotent within a stream.
- `GET operations?after={cursor}&limit={limit}` returns opaque operations in
  cursor order.
- `POST acknowledgements` records the last cursor processed by a device.
- `GET events?after={cursor}` is an SSE feed containing only
  `changes-available` cursor notifications; clients must pull operations.
- `POST mls/rotation-complete` reopens publishing after a confirmed MLS epoch
  rotation.

### Bootstrap and status response

`GET bootstrap` and `GET status` return:

```json
{
  "workspace_id": "550e8400-e29b-41d4-a716-446655440000",
  "app_id": "dev.solsynth.maidkit",
  "mls_group_id": "flywheel:550e8400-e29b-41d4-a716-446655440000:dev.solsynth.maidkit",
  "cursor": 4812,
  "mls_epoch": 7,
  "requires_mls_rotation": false
}
```

### Operation upload and pull

```http
POST .../operations
Content-Type: application/json

{
  "device_id": "local-mls-device-id",
  "operations": [
    {
      "operation_id": "f9c5aac3-efc3-4ca8-a21a-53231d5a9a84",
      "scheme_version": 1,
      "ciphertext": "base64-encoded-client-encrypted-payload"
    }
  ]
}
```

`scheme_version` is required positive, unencrypted metadata that identifies the
client-defined ciphertext format. It is preserved by the server and returned
by `GET operations?after={cursor}&limit={limit}`, allowing clients to process
mixed-version operation history during a crypto or payload migration. The
server does not interpret it or derive authorization from it.

The response contains the operation ID, sending device ID, scheme version,
assigned cursor, ciphertext, and creation time. `operation_id` retries return
the original cursor rather than creating a second operation. Ciphertext is
limited by `Flywheel:MaxOperationBytes` (1 MiB by default); pull pages are
capped by `Flywheel:MaxPullLimit` (500 by default).

### Errors clients should handle

| Status | Meaning | Client behavior |
| --- | --- | --- |
| 400 | Invalid package ID, cursor, or operation payload | Correct the request; do not retry unchanged |
| 403 | Not a required workspace role, or workspace is Free | Refresh workspace/plan state and stop sync |
| 404 | Unknown device or inaccessible stream resource | Re-bootstrap or register the device |
| 409 | Duplicate IDs within one batch, revoked-device conflict, or required MLS rotation | Pull/reconcile; complete MLS rotation before retrying uploads |

Viewer+ members can read, acknowledge, and subscribe. Member+ members can
register/revoke their own devices and publish operations. The server stores
ciphertext, cursor and retention metadata only; record types, clocks,
tombstones, conflict resolution, and client outboxes belong to each app.

Flywheel is available only to Pro and Enterprise Valve workspaces. Every API
request checks the current workspace plan, so Free workspaces cannot create,
read, subscribe to, or publish Flywheel data.

MLS group IDs are deterministic: `flywheel:{workspace-id}:{app-id}`. Flywheel
periodically reconciles Valve memberships and blocks new operations when a
member has been removed, until the MLS group reaches a newer committed epoch.
