# WattEngine API Documentation

## Overview

WattEngine is a .NET/C# microservices platform for the Solar Network ecosystem, providing workspace management, project boards (Broad), task management, and permission control.

**Services:**
- **WattEngine.Valve** - Workspace, permission, and billing management
- **WattEngine.Ideask** - Project boards (Broad) and task management
- **WattEngine.Flywheel** - End-to-end encrypted, package-scoped device sync

**Authentication:** All endpoints require JWT Bearer token via `Authorization` header (Solar Network Unified Authentication).

For the Flywheel sync contract and the required workspace setup flow, see
[Flywheel API](FLYWHEEL_API.md).

---

## WattEngine.Valve

### Workspace Endpoints

#### List My Workspaces
```
GET /api/workspaces
```
Returns all workspaces the authenticated user belongs to.

**Response:** `List<WtWorkspace>`

---

#### Get My Individual Workspace
```
GET /api/workspaces/individual
```
Returns the authenticated account's individual workspace.

Each account has exactly one active individual workspace. Valve creates it automatically when it receives the account-created event; clients cannot create an individual workspace through `POST /api/workspaces`.

**Response:** `WtWorkspace`
**Errors:** `404 Not Found` when the workspace has not yet been provisioned.

---

#### Get Workspace by Slug or ID
```
GET /api/workspaces/{slugOrId}
```
Returns a single workspace by its slug or GUID.

**Response:** `WtWorkspace`

| Field | Type | Description |
|-------|------|-------------|
| id | `Guid` | Unique identifier |
| slug | `string` | URL-friendly identifier (max 1024 chars) |
| name | `string` | Display name (max 1024 chars) |
| description | `string?` | Description (max 4096 chars) |
| type | `WorkspaceType` | `Individual` (0) or `Organization` (1) |
| ownerAccountId | `Guid` | Owner account ID |
| picture | `SnCloudFileReferenceObject?` | Workspace picture (jsonb) |
| background | `SnCloudFileReferenceObject?` | Workspace background (jsonb) |
| plan | `WorkspacePlan` | `Free` (0), `Pro` (1), or `Enterprise` (2) |
| planExpiresAt | `Instant?` | Plan expiration timestamp |
| isBundled | `bool` | Whether plan is bundled from perk |

---

#### Create Workspace
```
POST /api/workspaces
```
Creates a new organization workspace. The authenticated user becomes the owner. Individual workspaces are created automatically with the account and cannot be created through this endpoint.

**Request Body:**
```json
{
  "slug": "my-workspace",
  "name": "My Workspace",
  "description": "A workspace for my team",
  "type": 1,
  "pictureId": "cloud-file-id-for-icon",
  "backgroundId": "cloud-file-id-for-background"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| slug | `string` | Yes | Unique slug (max 1024 chars) |
| name | `string` | Yes | Display name (max 1024 chars) |
| description | `string?` | No | Description (max 4096 chars) |
| type | `WorkspaceType` | Yes | Must be `1` = Organization (`0` = Individual is rejected) |
| pictureId | `string?` | No | Cloud file ID for the workspace icon |
| backgroundId | `string?` | No | Cloud file ID for the workspace background |

**Response:** `201 Created` with `WtWorkspace`

---

#### Update Workspace
```
PATCH /api/workspaces/{slugOrId}
```
Updates workspace name and/or description. Requires Admin or Owner role.

**Request Body:**
```json
{
  "name": "Updated Name",
  "description": "Updated description",
  "pictureId": "cloud-file-id-for-icon",
  "backgroundId": "cloud-file-id-for-background"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| name | `string?` | No | New name (max 1024 chars) |
| description | `string?` | No | New description (max 4096 chars) |
| pictureId | `string?` | No | Cloud file ID for the workspace icon |
| backgroundId | `string?` | No | Cloud file ID for the workspace background |

**Response:** `WtWorkspace`

---

#### Delete Workspace
```
DELETE /api/workspaces/{slugOrId}
```
Deletes a workspace. Only the owner can delete.

**Response:** `204 No Content`

---

### Member Endpoints

#### List Members
```
GET /api/workspaces/{slugOrId}/members
```
Lists all members of a workspace. Requires at least Viewer role.

**Response:** `List<WtWorkspaceMember>`

| Field | Type | Description |
|-------|------|-------------|
| id | `Guid` | Unique identifier |
| workspaceId | `Guid` | Parent workspace ID |
| accountId | `Guid` | Member account ID |
| account | `SnAccount?` | Account details, including the profile, hydrated from the Profile service. `null` when the account cannot be found. |
| role | `int` | Role level (25=Viewer, 50=Member, 75=Admin, 100=Owner) |
| joinedAt | `Instant?` | When the member joined |
| leaveAt | `Instant?` | When the member left |

---

#### Invite Member
```
POST /api/workspaces/{slugOrId}/members/invite
```
Invites a user to the workspace. Requires Admin role. Only Owner can invite Admins. Individual workspaces may invite bot accounts only; a bot account has an `automatedId`.

**Request Body:**
```json
{
  "accountId": "550e8400-e29b-41d4-a716-446655440000",
  "role": 50
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| accountId | `Guid` | Yes | Account to invite; must identify a bot for an individual workspace |
| role | `int` | Yes | Role level (25, 50, 75, or 100) |

**Response:** `WtWorkspaceMember`

---

#### Update Member Role
```
PATCH /api/workspaces/{slugOrId}/members/{accountId}
```
Updates a member's role. Requires Admin role. Only Owner can assign Admin.

**Request Body:**
```json
{
  "role": 75
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| role | `int` | Yes | New role level |

**Response:** `WtWorkspaceMember`

---

#### Remove Member
```
DELETE /api/workspaces/{slugOrId}/members/{accountId}
```
Removes a member from the workspace. Requires Admin role. Cannot remove the owner.

**Response:** `204 No Content`

---

### Permission Endpoints

#### Check Permission
```
GET /api/workspaces/{slug}/permissions/check?key={permissionKey}
```
Checks if the authenticated user has a specific permission.

**Query Parameters:**
| Param | Type | Description |
|-------|------|-------------|
| key | `string` | Permission key (see Permission Keys below) |

**Response:**
```json
{
  "has_permission": true,
  "key": "ideask.use"
}
```

**Permission Keys:**
| Key | Description |
|-----|-------------|
| `workspace.manage` | Manage workspace settings |
| `workspace.members` | Manage members |
| `workspace.billing` | Manage billing |
| `projects.create` | Create projects |
| `projects.manage` | Manage projects |
| `ideask.use` | Use Ideask service |
| `drive.use` | Use Drive service |

---

#### Get All Role Permissions
```
GET /api/workspaces/{slug}/permissions/roles
```
Returns all role-level permission configurations. Requires at least Viewer role.

**Response:** `List<WtWorkspaceRolePermission>`

| Field | Type | Description |
|-------|------|-------------|
| id | `Guid` | Unique identifier |
| workspaceId | `Guid` | Parent workspace ID |
| roleLevel | `int` | Role level this applies to |
| canManageWorkspace | `bool` | Can manage workspace |
| canManageMembers | `bool` | Can manage members |
| canManageBilling | `bool` | Can manage billing |
| canCreateProjects | `bool` | Can create projects |
| canManageProjects | `bool` | Can manage projects |
| canUseIdeask | `bool` | Can use Ideask (default: true) |
| canUseDrive | `bool` | Can use Drive (default: true) |

---

#### Get Role Permission
```
GET /api/workspaces/{slug}/permissions/roles/{roleLevel}
```
Returns permission configuration for a specific role level.

**Response:** `WtWorkspaceRolePermission`

---

#### Update Role Permission
```
PUT /api/workspaces/{slug}/permissions/roles/{roleLevel}
```
Updates permission configuration for a role level. Only Owner can modify.

**Request Body:**
```json
{
  "canManageWorkspace": false,
  "canManageMembers": true,
  "canManageBilling": false,
  "canCreateProjects": true,
  "canManageProjects": true,
  "canUseIdeask": true,
  "canUseDrive": true
}
```

| Field | Type | Description |
|-------|------|-------------|
| canManageWorkspace | `bool` | Can manage workspace |
| canManageMembers | `bool` | Can manage members |
| canManageBilling | `bool` | Can manage billing |
| canCreateProjects | `bool` | Can create projects |
| canManageProjects | `bool` | Can manage projects |
| canUseIdeask | `bool` | Can use Ideask |
| canUseDrive | `bool` | Can use Drive |

**Response:** `WtWorkspaceRolePermission`

---

#### Get User Permission
```
GET /api/workspaces/{slug}/permissions/users/{accountId}
```
Returns user-specific permission overrides. Requires Admin role.

**Response:** `WtWorkspaceUserPermission`

| Field | Type | Description |
|-------|------|-------------|
| id | `Guid` | Unique identifier |
| workspaceId | `Guid` | Parent workspace ID |
| accountId | `Guid` | Target account ID |
| canManageWorkspace | `bool?` | Override: can manage workspace (null = use role default) |
| canManageMembers | `bool?` | Override: can manage members |
| canManageBilling | `bool?` | Override: can manage billing |
| canCreateProjects | `bool?` | Override: can create projects |
| canManageProjects | `bool?` | Override: can manage projects |
| canUseIdeask | `bool?` | Override: can use Ideask |
| canUseDrive | `bool?` | Override: can use Drive |

---

#### Update User Permission
```
PUT /api/workspaces/{slug}/permissions/users/{accountId}
```
Updates user-specific permission overrides. Requires Admin role.

**Request Body:** Same as `WtWorkspaceUserPermission` (all fields nullable bool).

**Response:** `WtWorkspaceUserPermission`

---

### Plan & Quota Endpoints

#### Get Plan Status
```
GET /api/workspaces/{slug}/plan/status
```
Returns current plan status including bundled plan info and pricing.

**Response:**
```json
{
  "plan": 1,
  "planExpiresAt": null,
  "isBundled": false,
  "bundled_plan": {
    "isEnabled": true,
    "workspace_id": "550e8400-...",
    "lastReassignedAt": null,
    "cooldown_active": false
  },
  "prices": {
    "pro": 100,
    "enterprise": 500,
    "currency": "golds"
  }
}
```

---

#### Get Quota
```
GET /api/workspaces/{slug}/quota
```
Returns resource quotas for the workspace based on its plan.

**Response:**
```json
{
  "plan": 1,
  "quotas": {
    "max_projects": 20,
    "max_members": 50,
    "max_tasks_per_project": 1000,
    "max_broads_per_project": 50,
    "max_storage_bytes": 10737418240
  }
}
```

**Quota Limits by Plan:**

| Resource | Free | Pro | Enterprise |
|----------|------|-----|------------|
| max_projects | 3 | 20 | 100 |
| max_members | 5 | 50 | 500 |
| max_tasks_per_project | 100 | 1,000 | 10,000 |
| max_broads_per_project | 5 | 50 | 200 |
| max_storage_bytes | 1 GB | 10 GB | 100 GB |

---

#### Check Specific Quota
```
GET /api/workspaces/{slug}/quota/check?resource={resourceType}
```
Returns the quota limit for a specific resource type.

**Query Parameters:**
| Param | Type | Description |
|-------|------|-------------|
| resource | `string` | One of: `projects`, `members`, `tasks`, `broads`, `storage` |

**Response:**
```json
{
  "resource": "projects",
  "limit": 20,
  "plan": 1
}
```

---

#### Assign Bundled Plan
```
POST /api/workspaces/{slugOrId}/plan/assign-bundled
```
Assigns a bundled Pro plan to an individual workspace. Requires Owner and perk level 3+. Eligible accounts have the bundled Pro plan automatically assigned when their individual workspace is provisioned; organization workspaces cannot receive bundled Pro plans.

**Response:** `WtWorkspaceBundledPlan`

---

#### Unassign Bundled Plan
```
POST /api/workspaces/{slugOrId}/plan/unassign-bundled
```
Removes the bundled plan assignment.

**Response:**
```json
{
  "message": "Bundled plan unassigned."
}
```

---

#### Reassign Bundled Plan
```
POST /api/workspaces/plan/reassign-bundled
```
Reassigns bundled plan to a different individual workspace owned by the account. Subject to 7-day cooldown. Organization workspaces are rejected.

**Request Body:**
```json
{
  "workspaceId": "550e8400-e29b-41d4-a716-446655440000"
}
```

---

#### Subscribe to Plan
```
POST /api/workspaces/{slugOrId}/plan/subscribe
```
Creates a paid plan subscription order. Only Owner can manage subscriptions.

**Request Body:**
```json
{
  "plan": 1
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| plan | `WorkspacePlan` | Yes | `1` = Pro, `2` = Enterprise |

**Response:**
```json
{
  "order_id": "550e8400-...",
  "amount": 100,
  "currency": "golds",
  "plan": 1
}
```

---

## WattEngine.Ideask

### Broad (Project Board) Endpoints

#### List Broads
```
GET /api/broads
```
Returns all broads accessible to the authenticated user.

**Response:** `List<WtBroad>`

| Field | Type | Description |
|-------|------|-------------|
| id | `Guid` | Unique identifier |
| name | `string` | Broad name (max 256 chars) |
| accountId | `Guid` | Creator account ID |
| workspaceId | `Guid?` | Associated workspace ID |
| taskPrefix | `string?` | Optional 1-32 character task-reference prefix, such as `SN`. Task references are derived as `SN-1`, `SN-2`, and so on. |
| visibility | `Visibility` | `Private` (0) or `Public` (1) |
| description | `string?` | Description (max 8192 chars) |
| content | `string?` | Optional long-form task detail (rich text) |
| backgroundImage | `SnCloudFileReferenceObject?` | Background image (jsonb) |
| iconImage | `SnCloudFileReferenceObject?` | Icon image (jsonb) |

---

#### Get Broad
```
GET /api/broads/{broadId}
```
Returns a single broad by ID.

**Response:** `WtBroad`

---

#### Create Broad
```
POST /api/broads
```
Creates a new broad (project board).

**Request Body:**
```json
{
  "name": "My Project Board",
  "description": "Tracking project progress",
  "content": "",
  "visibility": 0,
  "workspaceId": "550e8400-e29b-41d4-a716-446655440000",
  "backgroundImageId": "cloud-file-id-for-background",
  "iconImageId": "cloud-file-id-for-icon"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| name | `string` | Yes | Name (1-256 chars) |
| description | `string?` | No | Description (max 8192 chars) |
| content | `string?` | No | Optional long-form task detail (rich text) |
| visibility | `Visibility?` | No | `0` = Private, `1` = Public |
| workspaceId | `Guid?` | No | Associate with workspace |
| taskPrefix | `string?` | No | Optional task-reference prefix. It is normalized to uppercase; tasks store only their generated number. |
| backgroundImageId | `string?` | No | Cloud file ID for the board background |
| iconImageId | `string?` | No | Cloud file ID for the board icon |

**Response:** `201 Created` with `WtBroad`

---

#### Update Broad
```
PATCH /api/broads/{broadId}
```
Updates a broad.

**Request Body:**
```json
{
  "name": "Updated Board Name",
  "description": "Updated description",
  "content": "Updated content",
  "visibility": 1,
  "workspaceId": "550e8400-...",
  "backgroundImageId": "cloud-file-id-for-background",
  "iconImageId": "cloud-file-id-for-icon"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| name | `string` | Yes | Name (1-256 chars) |
| description | `string?` | No | Description (max 8192 chars) |
| content | `string?` | No | Optional long-form task detail (rich text) |
| visibility | `Visibility?` | No | `0` = Private, `1` = Public |
| workspaceId | `Guid?` | No | Associate with workspace |
| taskPrefix | `string?` | No | Set the task-reference prefix. Existing task numbers remain unchanged. |
| clearTaskPrefix | `bool?` | No | Set to `true` to remove the prefix and show only the generated task number. |
| backgroundImageId | `string?` | No | Cloud file ID for the board background; omitted leaves the current value unchanged |
| iconImageId | `string?` | No | Cloud file ID for the board icon; omitted leaves the current value unchanged |

**Response:** `WtBroad`

---

#### Delete Broad
```
DELETE /api/broads/{broadId}
```
Deletes a broad.

**Response:** `204 No Content`

---

### Task Endpoints

#### List Tasks in Broad
```
GET /api/broads/{broadId}/tasks
```
Returns all tasks within a broad.

**Response:** `List<WtTask>`

| Field | Type | Description |
|-------|------|-------------|
| id | `Guid` | Unique identifier |
| serialNumber | `int` | Immutable number generated automatically within the board |
| taskKey | `string` | Derived display reference, using the board prefix when configured (for example `SN-1`) |
| name | `string` | Task name (max 4096 chars) |
| description | `string?` | Description (max 8192 chars) |
| content | `string?` | Rich text content |
| attachments | `List<SnCloudFileReferenceObject>` | File attachments (jsonb) |
| tags | `List<string>` | Optional task tags |
| priority | `int` | Priority level (0+) |
| deadlineAt | `Instant?` | Deadline timestamp |
| completedAt | `Instant?` | Completion timestamp |
| completeReason | `TaskCompleteReason?` | `Completed` (0), `Skipped` (1), `Duplicated` (2) |
| broadId | `Guid` | Parent broad ID |
| parentTaskId | `Guid?` | Parent task ID (for subtasks) |
| groupId | `Guid?` | Optional task group; null means ungrouped |

---

#### Get Task
```
GET /api/tasks/{taskId}
```
Returns a single task by ID.

**Response:** `WtTask`

---

#### Create Task
```
POST /api/broads/{broadId}/tasks
```
Creates a new task in a broad.

**Request Body:**
```json
{
  "name": "Implement feature X",
  "description": "Detailed description",
  "content": "",
  "attachmentIds": ["file-id-1", "file-id-2"],
  "priority": 1,
  "deadlineAt": 1700000000,
  "parentTaskId": null,
  "assigneeAccountIds": ["550e8400-..."],
  "groupId": "550e8400-...",
  "tags": ["backend", "urgent"]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| name | `string` | Yes | Name (1-1024 chars) |
| description | `string?` | No | Description (max 8192 chars) |
| content | `string?` | No | Rich text content |
| attachmentIds | `List<string>?` | No | File attachment IDs |
| priority | `int` | Yes | Priority (>= 0) |
| deadlineAt | `Instant?` | No | Deadline timestamp |
| parentTaskId | `Guid?` | No | Parent task for subtasks |
| assigneeAccountIds | `List<Guid>?` | No | Initial assignees |
| groupId | `Guid?` | No | Optional task group; omitted leaves the task ungrouped |
| tags | `List<string>?` | No | Optional tags (each 1-128 characters) |

**Response:** `201 Created` with `WtTask`

---

#### Update Task
```
PATCH /api/tasks/{taskId}
```
Updates a task.

**Request Body:**
```json
{
  "name": "Updated task name",
  "description": "Updated description",
  "content": "Updated content",
  "attachmentIds": ["file-id-3"],
  "priority": 2,
  "deadlineAt": 1700000000,
  "completeReason": 0,
  "ungroup": true,
  "tags": ["backend"]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| name | `string` | Yes | Name (1-1024 chars) |
| description | `string?` | No | Description (max 8192 chars) |
| content | `string?` | No | Rich text content |
| attachmentIds | `List<string>?` | No | File attachment IDs |
| priority | `int?` | No | Priority (>= 0) |
| deadlineAt | `Instant?` | No | Deadline timestamp |
| completeReason | `TaskCompleteReason?` | No | Mark as completed/skipped/duplicated |
| groupId | `Guid?` | No | Move to this group |
| ungroup | `bool?` | No | `true` removes the task from its group |
| tags | `List<string>?` | No | Replaces tags; an empty list clears them |

**Response:** `WtTask`

---

### Task Group Endpoints

#### List Task Groups
```
GET /api/broads/{broadId}/groups
```
Lists the board's task groups in position order.

#### Create Task Group
```
POST /api/broads/{broadId}/groups
```
```json
{ "name": "In Progress", "position": 1 }
```
Both `position` and task membership are optional; tasks without a `groupId` remain ungrouped.

#### Update or Delete Task Group
```
PATCH /api/task-groups/{groupId}
DELETE /api/task-groups/{groupId}
```
Deleting a group leaves its tasks intact and ungrouped.

---

#### Delete Task
```
DELETE /api/tasks/{taskId}
```
Deletes a task.

**Response:** `204 No Content`

---

#### Assign Users to Task
```
POST /api/tasks/{taskId}/assignees
```
Assigns users to a task.

**Request Body:**
```json
{
  "assigneeAccountIds": ["550e8400-e29b-41d4-a716-446655440000"]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| assigneeAccountIds | `List<Guid>` | Yes | Account IDs to assign |

**Response:** `204 No Content`

---

#### Unassign User from Task
```
DELETE /api/tasks/{taskId}/assignees/{assigneeAccountId}
```
Removes a user assignment from a task.

**Response:** `204 No Content`

---

## Platform Admin Endpoints

Platform-level administration for WattEngine services. Unlike the workspace- and account-scoped
endpoints above, these require a **platform permission node** (see [Permission nodes](#permission-nodes))
checked against Padlock by `RemotePermissionMiddleware` — workspace membership is **not** required.
All routes are under `/api/admin` and are gated by `[Authorize]` plus one or more
`[AskPermission(...)]` attributes; they return `401`/`403` without the granting node.

### Permission Nodes

Nodes are defined as constants in `DysonNetwork.Shared/Auth/PermissionKeys.cs` and auto-seeded into
the Padlock `default` permission group at startup (reflection over the registry). Deliverable shape:
`{domain}.{resource}.{action}`.

| Node | Description |
|------|-------------|
| `workspaces.view` | List / inspect any workspace |
| `workspaces.manage` | Update any workspace |
| `workspaces.delete` | Soft-delete any workspace |
| `workspaces.plans.manage` | Override a workspace's plan |
| `boards.view` | List / inspect any board |
| `boards.manage` | Update any board |
| `boards.delete` | Delete any board |
| `tasks.view` | List / inspect any task |
| `tasks.manage` | Update any task |
| `tasks.delete` | Delete any task |
| `tasks.integrations.manage` | List / remove GitHub integrations |
| `flywheel.view` | Flywheel usage stats and app inventory |
| `flywheel.apps.manage` | Override app retention settings |
| `flywheel.blobs.delete` | Purge a blob and its revisions |
| `flywheel.audit.view` | Read Flywheel audit log |

### WattEngine.Valve Admin

#### List Workspaces

`GET /api/admin/workspaces?type=&plan=&q=&includeDeleted=&take=50&offset=0`

Gated by `workspaces.view`. Supports `type` / `plan` enum filters, `q` slug/name search,
`includeDeleted`, and pagination (`X-Total` header). Field `member_count` reflects active members.

#### Get Workspace Detail

`GET /api/admin/workspaces/{id}` — workspace, active members, role permissions, user
permission overrides, and bundled plans. Gated by `workspaces.view`.

#### Update Workspace

`PATCH /api/admin/workspaces/{id}` — update `name`, `slug`, `description`. Gated by `workspaces.manage`.
Slug conflicts return `409`.

#### Update Workspace Plan

`PUT /api/admin/workspaces/{id}/plan` — override `plan`, `plan_expires_at`, `is_bundled`.
Gated by `workspaces.plans.manage`.

#### Delete Workspace

`DELETE /api/admin/workspaces/{id}` — soft-delete the workspace. Gated by `workspaces.delete`.

#### Get Workspace Stats

`GET /api/admin/stats` — totals and breakdowns by workspace type/plan. Gated by `workspaces.view`.

### WattEngine.Ideask Admin

#### List Boards

`GET /api/admin/boards?workspaceId=&accountId=&visibility=&q=&includeDeleted=&take=50&offset=0`
Gated by `boards.view`. Each item includes its current task count.

#### Get Board Detail

`GET /api/admin/boards/{id}` — the board plus a summary of its tasks. Gated by `boards.view`.

#### Update Board

`PATCH /api/admin/boards/{id}` — update `name`, `description`, `visibility`, `task_prefix`.
Gated by `boards.manage`.

#### Delete Board

`DELETE /api/admin/boards/{id}` — soft-delete the board and its tasks. Gated by `boards.delete`.

#### List Tasks

`GET /api/admin/tasks?broadId=&status=&groupId=&q=&includeDeleted=&take=50&offset=0`
Gated by `tasks.view`. `status` is one of `Open`, `Completed`, `Skipped`, `Duplicated`.

#### Get Task Detail

`GET /api/admin/tasks/{id}` — the task, assignees, comments, and GitHub issue links. Gated by `tasks.view`.

#### Update Task

`PATCH /api/admin/tasks/{id}` — update `name`, `description`, `priority`, `deadline_at`, and
completion state (`complete: true/false`). Gated by `tasks.manage`.

#### Delete Task

`DELETE /api/admin/tasks/{id}` — soft-delete the task. Gated by `tasks.delete`.

#### List / Remove GitHub Integrations

`GET /api/admin/github-integrations?broadId=&q=&includeDeleted=&take=50&offset=0` and
`DELETE /api/admin/github-integrations/{id}` — inspect sync state (incl. `last_error`) and remove
misbehaving integrations and their linked issues/comments. Gated by `tasks.integrations.manage`.

### WattEngine.Flywheel Admin

#### Get Flywheel Stats

`GET /api/admin/flywheel/stats` — distinct workspaces, app settings, blobs, revisions, total
retained bytes, and audit counts. Gated by `flywheel.view`.

#### List App Settings

`GET /api/admin/flywheel/apps?workspaceId=&take=50&offset=0` — per-app settings with blob/revision
counts and retained bytes. Gated by `flywheel.view`.

#### Inspect Audit Log

`GET /api/admin/flywheel/audit?workspaceId=&appId=&take=50&offset=0` — metadata-only audit trail
(never blob bytes or storage keys). Gated by `flywheel.audit.view`.

#### Override App Retention

`PATCH /api/admin/flywheel/apps/{id}` — set `retained_revision_count` (admin override, not capped by
workspace plan). Gated by `flywheel.apps.manage`.

#### Purge Blob

`DELETE /api/admin/flywheel/blobs/{blobId}?workspaceId={id}&appId={app}` — removes the blob, all its
S3 objects and revisions, and writes a `blob.admin_deleted` audit entry. Gated by `flywheel.blobs.delete`.

---

## gRPC Services (Inter-Service)

### WorkspaceGrpcService

#### CheckPermission
Checks if an account has a specific permission in a workspace.

**Request:** `CheckPermissionRequest`
| Field | Type | Description |
|-------|------|-------------|
| workspaceId | `string` | Workspace GUID |
| accountId | `string` | Account GUID |
| permission | `string` | Permission key |

**Response:** `CheckPermissionResponse`
| Field | Type | Description |
|-------|------|-------------|
| hasPermission | `bool` | Whether permission is granted |

---

#### GetWorkspace
Retrieves workspace information by ID.

**Request:** `GetWorkspaceRequest`
| Field | Type | Description |
|-------|------|-------------|
| id | `string` | Workspace GUID |

**Response:** `GetWorkspaceResponse`
| Field | Type | Description |
|-------|------|-------------|
| id | `string` | Workspace GUID |
| slug | `string` | Workspace slug |
| name | `string` | Workspace name |
| type | `int` | Workspace type |
| ownerAccountId | `string` | Owner account GUID |

---

#### GetIndividualWorkspace
Retrieves the active individual workspace owned by an account.

**Request:** `DyGetUserWorkspacesRequest`
| Field | Type | Description |
|-------|------|-------------|
| accountId | `string` | Account GUID |

**Response:** `DyWorkspace`

Returns `NotFound` when the account has no provisioned individual workspace and `InvalidArgument` for a malformed account ID.

---

#### IsMember
Checks if an account is a member of a workspace.

**Request:** `IsMemberRequest`
| Field | Type | Description |
|-------|------|-------------|
| workspaceId | `string` | Workspace GUID |
| accountId | `string` | Account GUID |

**Response:** `IsMemberResponse`
| Field | Type | Description |
|-------|------|-------------|
| isMember | `bool` | Whether the account is a member |
| role | `int` | Member role level (0 if not a member) |

---

## WebSocket Events (Ideask)

Real-time events are delivered via WebSocket with the following packet structure:

```json
{
  "type": "ideask.{eventType}",
  "data": {
    "entity": "task|broad",
    "data": { ... },
    "timestamp": 1700000000,
    "triggered_by": "550e8400-..."
  }
}
```

### Event Types

| Event | Entity | Payload |
|-------|--------|---------|
| `task.created` | `task` | `TaskCreatedPayload` |
| `task.updated` | `task` | `TaskUpdatedPayload` |
| `task.assigned` | `task` | `TaskAssignedPayload` |
| `task.due_reminder` | `task` | `TaskDueReminderPayload` |
| `broad.created` | `broad` | `BroadCreatedPayload` |
| `broad.updated` | `broad` | `BroadUpdatedPayload` |

### TaskAssignedPayload
```json
{
  "task": { },
  "broad": { },
  "assignedUserIds": ["550e8400-..."],
  "unassignedUserIds": ["550e8400-..."]
}
```

### TaskDueReminderPayload
```json
{
  "task": { },
  "broad": { },
  "timeUntilDue": { },
  "reminderLevel": "warning|overdue"
}
```

### TaskCreatedPayload / TaskUpdatedPayload
```json
{
  "task": { },
  "broad": { },
  "changedProperties": ["name", "priority"]
}
```

### BroadCreatedPayload / BroadUpdatedPayload
```json
{
  "broad": { },
  "changedProperties": ["name", "description"]
}
```

---

## Permission Resolution Order

1. **User-specific override** (`WtWorkspaceUserPermission`) - checked first
2. **Role-level permission** (`WtWorkspaceRolePermission`) - checked if no user override
3. **Default role permission** - fallback based on role level

### Default Permissions by Role

| Permission | Owner | Admin | Member | Viewer |
|------------|-------|-------|--------|--------|
| workspace.manage | Yes | No | No | No |
| workspace.members | Yes | Yes | No | No |
| workspace.billing | Yes | No | No | No |
| projects.create | Yes | Yes | Yes | No |
| projects.manage | Yes | Yes | No | No |
| ideask.use | Yes | Yes | Yes | Yes |
| drive.use | Yes | Yes | Yes | Yes |

---

## Enums

### WorkspaceType
| Value | Name |
|-------|------|
| 0 | Individual |
| 1 | Organization |

### WorkspacePlan
| Value | Name | Monthly Price |
|-------|------|---------------|
| 0 | Free | 0 |
| 1 | Pro | 100 golds |
| 2 | Enterprise | 500 golds |

### Visibility
| Value | Name |
|-------|------|
| 0 | Private |
| 1 | Public |

### TaskCompleteReason
| Value | Name |
|-------|------|
| 0 | Completed |
| 1 | Skipped |
| 2 | Duplicated |

### WorkspaceMemberRole
| Value | Name |
|-------|------|
| 100 | Owner |
| 75 | Admin |
| 50 | Member |
| 25 | Viewer |
