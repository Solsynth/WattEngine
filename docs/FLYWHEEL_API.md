# WattEngine Flywheel API

Flywheel is an opaque, end-to-end encrypted operation transport. A stream is
always scoped to a Valve workspace and an application package identifier:

```text
/api/flywheel/workspaces/{workspaceId}/apps/{appId}
```

`appId` is required and must be the reverse-DNS application identifier used by
Ring and the client WebSocket namespace, for example `dev.solsynth.maidkit`.
There is no default app identifier and no cross-package access path.

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

Viewer+ members can read, acknowledge, and subscribe. Member+ members can
register/revoke their own devices and publish operations. The server stores
ciphertext, cursor and retention metadata only; record types, clocks,
tombstones, conflict resolution, and client outboxes belong to each app.

MLS group IDs are deterministic: `flywheel:{workspace-id}:{app-id}`. Flywheel
periodically reconciles Valve memberships and blocks new operations when a
member has been removed, until the MLS group reaches a newer committed epoch.
