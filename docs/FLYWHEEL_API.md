# WattEngine Flywheel API

Flywheel is a versioned opaque-blob relay. Apps encrypt archives locally,
upload the resulting bytes, and decrypt/import downloaded bytes locally.
Flywheel stores only blob IDs, revisions, sizes, SHA-256 checksums, client
scheme versions, and private S3 object keys.

The public gateway path is:

```text
https://api.solian.app/flywheel/workspaces/{workspaceId}/apps/{appId}
```

The gateway rewrites `/flywheel` to Flywheel's internal `/api` controller path.
`workspaceId` is a Valve UUID and `appId` is a required reverse-DNS package ID,
such as `dev.solsynth.maidkit`.

## Access and retention

- Flywheel requires a Pro or Enterprise Valve workspace.
- Viewer+ may list, inspect, download, and receive SSE notifications.
- Member+ may upload encrypted blobs.
- Admin/Owner may configure retained prior revisions per workspace × app.
- Workspace Owners may inventory every Flywheel app/save, inspect audit metadata,
  and permanently delete an individual opaque save.
- Pro permits `0–3`; Enterprise permits `0–20`. `0` keeps only the current
  revision.

## Blob API

- `GET settings` / `PATCH settings` read or set `retained_revision_count`.
- `GET blobs` lists known opaque blob IDs and their current revisions.
- `GET blobs/{blobId}` reads current metadata.
- `PUT blobs/{blobId}` uploads a multipart encrypted archive.
- `GET blobs/{blobId}/revisions/{revision}` reads immutable revision metadata.
- `GET blobs/{blobId}/content?revision={optionalRevision}` downloads encrypted
  bytes; without `revision`, it downloads the current version.
- `GET events?after={eventCursor}` opens an SSE stream. Events contain only a
  blob ID and revision, so clients must re-download data themselves.

### Upload

```http
PUT .../blobs/550e8400-e29b-41d4-a716-446655440000
Content-Type: multipart/form-data

file=<encrypted archive bytes>
scheme_version=1
expected_revision=0
```

`expected_revision=0` creates a new blob. Later uploads must supply the
currently downloaded revision. A stale upload receives `409 Conflict`; the
client should download the current archive, decrypt/import or resolve it
locally, then export and retry with the new revision.

## MaidKit integration

MaidKit assigns each vault a stable opaque blob UUID. It uses its existing
`DatabaseBackupService.exportArchive` and `importArchive` methods with the
shared sync passphrase. The passphrase and plaintext never leave the client.
When SSE reports a newer revision, MaidKit downloads the archive and asks the
client flow to decrypt/import it; it does not treat an SSE event as data.

## Workspace-owner management API

The owner-only gateway path is `GET /flywheel/workspaces/{workspaceId}/apps`.
It lists every app namespace with retained-byte and revision totals, but never
returns encrypted content or S3 keys. Owners can also use:

- `GET .../apps/{appId}/management/blobs` for opaque save inventory.
- `GET .../apps/{appId}/management/audit?take=100` for metadata audit events.
  Every `blob.uploaded` event includes the uploader account ID, blob UUID,
  revision, and timestamp. Retention changes and owner deletions are recorded
  too.
- `DELETE .../apps/{appId}/management/blobs/{blobId}` permanently deletes every
  retained revision of that opaque save. This streams deletion to private S3
  before removing the database metadata; it cannot be undone.
