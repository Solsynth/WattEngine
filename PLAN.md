# WattEngine.Valve - Workspace & Permission Management

## Context

WattEngine needs a centralized service to manage workspaces, permissions, and billing for all WattEngine services. Currently, DysonNetwork.Passport has a Realm system that handles similar functionality for DysonNetwork. We'll create a new `WattEngine.Valve` project that follows the same patterns but is tailored for WattEngine's workspace-based permission model.

**Key Requirements:**
- Workspaces can be **individual** (personal) or **organization** (team)
- All WattEngine service permissions belong to workspaces
- Billing management per workspace
- Reference DysonNetwork.Passport's Realm system for patterns

## Approach

Create a new `WattEngine.Valve` project following the existing WattEngine.Ideask structure, reusing DysonNetwork.Shared utilities.

## Files to Modify

### New Files (WattEngine.Valve/)
- `WattEngine.Valve.csproj` - Project file
- `Program.cs` - Entry point
- `AppDatabase.cs` - EF Core context
- `appsettings.json` - Configuration
- `Startup/ServiceCollectionExtensions.cs` - DI setup
- `Startup/ApplicationBuilderExtensions.cs` - Middleware setup
- `Startup/ScheduledJobsConfiguration.cs` - Quartz jobs
- `Workspace/WorkspaceModel.cs` - Workspace entities
- `Workspace/WorkspaceService.cs` - Business logic
- `Workspace/WorkspaceController.cs` - REST API
- `Workspace/WorkspaceServiceGrpc.cs` - gRPC service
- `Permission/PermissionModel.cs` - Permission entities
- `Permission/PermissionService.cs` - Permission logic
- `Permission/PermissionController.cs` - REST API
- `Billing/BillingModel.cs` - Billing entities
- `Billing/BillingService.cs` - Billing logic
- `Billing/BillingController.cs` - REST API
- `Dockerfile` - Container config

### Modified Files
- `WattEngine.sln` - Add new project

## Reuse from DysonNetwork.Shared

- `ModelBase` - Base entity with audit fields
- `ICacheService` - Redis caching
- `DyAccountService` - Account lookups via gRPC
- `RemotePermissionMiddleware` - Permission checking pattern
- `ResourceQuotaCalculator` - Quota management pattern
- `FlushBufferService` - Batched writes
- `PaginationExtensions` - Pagination helpers

## Data Models

### Workspace
```csharp
public class WtWorkspace : ModelBase
{
    public Guid Id { get; set; }
    public string Slug { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public WorkspaceType Type { get; set; } // Individual, Organization
    public Guid OwnerAccountId { get; set; }
    public SnCloudFileReferenceObject? Picture { get; set; }
    public SnCloudFileReferenceObject? Background { get; set; }
    public WorkspacePlan Plan { get; set; } // Free, Pro, Enterprise
    public Instant? PlanExpiresAt { get; set; }
    public List<WtWorkspaceMember> Members { get; set; }
    public List<WtWorkspaceRolePermission> RolePermissions { get; set; }
}

public enum WorkspaceType { Individual = 0, Organization = 1 }
public enum WorkspacePlan { Free = 0, Pro = 1, Enterprise = 2 }
```

### WorkspaceMember
```csharp
public class WtWorkspaceMember : ModelBase
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid AccountId { get; set; }
    public WorkspaceMemberRole Role { get; set; }
    public Instant? JoinedAt { get; set; }
    public Instant? LeaveAt { get; set; }
}

public static class WorkspaceMemberRole
{
    public const int Owner = 100;
    public const int Admin = 75;
    public const int Member = 50;
    public const int Viewer = 25;
}
```

### WorkspaceRolePermission
```csharp
public class WtWorkspaceRolePermission : ModelBase
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public int RoleLevel { get; set; }
    
    // Service-specific permissions
    public bool CanManageWorkspace { get; set; }
    public bool CanManageMembers { get; set; }
    public bool CanManageBilling { get; set; }
    public bool CanCreateProjects { get; set; }
    public bool CanManageProjects { get; set; }
    public bool CanUseIdeask { get; set; }
    public bool CanUseDrive { get; set; }
    // ... more service permissions as needed
}
```

### WorkspaceUserPermission (overrides)
```csharp
public class WtWorkspaceUserPermission : ModelBase
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid AccountId { get; set; }
    
    // Nullable overrides (null = use role default)
    public bool? CanManageWorkspace { get; set; }
    public bool? CanManageMembers { get; set; }
    // ... etc
}
```

### Billing
```csharp
public class WtWorkspaceBilling : ModelBase
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string PaymentMethodId { get; set; }
    public Instant? NextBillingAt { get; set; }
    public decimal MonthlyAmount { get; set; }
    public string Currency { get; set; } = "usd";
}

public class WtWorkspaceInvoice : ModelBase
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public InvoiceStatus Status { get; set; }
    public Instant PaidAt { get; set; }
}
```

## API Endpoints

### Workspace Management
- `GET /api/workspaces` - List user's workspaces
- `GET /api/workspaces/{slug}` - Get workspace by slug
- `POST /api/workspaces` - Create workspace
- `PATCH /api/workspaces/{slug}` - Update workspace
- `DELETE /api/workspaces/{slug}` - Delete workspace

### Member Management
- `GET /api/workspaces/{slug}/members` - List members
- `POST /api/workspaces/{slug}/members/invite` - Invite member
- `PATCH /api/workspaces/{slug}/members/{accountId}` - Update member role
- `DELETE /api/workspaces/{slug}/members/{accountId}` - Remove member

### Permission Management
- `GET /api/workspaces/{slug}/permissions/roles` - Get role permissions
- `PUT /api/workspaces/{slug}/permissions/roles/{roleLevel}` - Update role permissions
- `GET /api/workspaces/{slug}/permissions/users/{accountId}` - Get user permissions
- `PUT /api/workspaces/{slug}/permissions/users/{accountId}` - Update user permissions
- `GET /api/workspaces/{slug}/permissions/check` - Check current user permission

### Billing
- `GET /api/workspaces/{slug}/billing` - Get billing info
- `POST /api/workspaces/{slug}/billing/subscribe` - Subscribe to plan
- `POST /api/workspaces/{slug}/billing/cancel` - Cancel subscription
- `GET /api/workspaces/{slug}/billing/invoices` - List invoices

## Steps

- [x] 1. Create WattEngine.Valve project structure
- [x] 2. Add project to WattEngine.sln
- [x] 3. Create data models (Workspace, Member, Permission, Billing)
- [x] 4. Create AppDatabase with EF Core configuration
- [x] 5. Create WorkspaceService with CRUD operations
- [x] 6. Create WorkspaceController with REST endpoints
- [x] 7. Create PermissionService with role/user permission logic
- [x] 8. Create PermissionController with permission endpoints
- [x] 9. Create BillingService with subscription management
- [x] 10. Create BillingController with billing endpoints
- [x] 11. Create gRPC service for inter-service permission checks
- [x] 12. Configure startup and middleware
- [x] 13. Create Dockerfile
- [x] 14. Add EF Core migration

## Verification

1. Build the project: `dotnet build WattEngine.Valve/WattEngine.Valve.csproj`
2. Run database migration: `dotnet ef migrations add Initial -p WattEngine.Valve`
3. Test API endpoints with Swagger/Postman
4. Verify gRPC service works with other WattEngine services
