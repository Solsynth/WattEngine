using Grpc.Core;

namespace WattEngine.Valve.Workspace;

// ponytail: simplified gRPC service - add proto definitions later for proper gRPC
public class WorkspaceGrpcService(
    WorkspaceService ws,
    PermissionService perms
)
{
    public async Task<CheckPermissionResponse> CheckPermission(
        CheckPermissionRequest request, ServerCallContext context)
    {
        var workspaceId = Guid.Parse(request.WorkspaceId);
        var accountId = Guid.Parse(request.AccountId);

        var hasPermission = await perms.HasPermission(workspaceId, accountId, request.Permission);

        return new CheckPermissionResponse
        {
            HasPermission = hasPermission
        };
    }

    public async Task<GetWorkspaceResponse> GetWorkspace(
        GetWorkspaceRequest request, ServerCallContext context)
    {
        var workspace = await ws.GetById(Guid.Parse(request.Id))
            ?? throw new RpcException(new Status(StatusCode.NotFound, "Workspace not found"));

        return new GetWorkspaceResponse
        {
            Id = workspace.Id.ToString(),
            Slug = workspace.Slug,
            Name = workspace.Name,
            Type = (int)workspace.Type,
            OwnerAccountId = workspace.OwnerAccountId.ToString()
        };
    }

    public async Task<IsMemberResponse> IsMember(
        IsMemberRequest request, ServerCallContext context)
    {
        var workspaceId = Guid.Parse(request.WorkspaceId);
        var accountId = Guid.Parse(request.AccountId);

        var member = await ws.GetMember(workspaceId, accountId);

        return new IsMemberResponse
        {
            IsMember = member != null,
            Role = member?.Role ?? 0
        };
    }
}

// ponytail: placeholder message types until proto generation is set up
public class CheckPermissionRequest
{
    public string WorkspaceId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string Permission { get; set; } = string.Empty;
}

public class CheckPermissionResponse
{
    public bool HasPermission { get; set; }
}

public class GetWorkspaceRequest
{
    public string Id { get; set; } = string.Empty;
}

public class GetWorkspaceResponse
{
    public string Id { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Type { get; set; }
    public string OwnerAccountId { get; set; } = string.Empty;
}

public class IsMemberRequest
{
    public string WorkspaceId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
}

public class IsMemberResponse
{
    public bool IsMember { get; set; }
    public int Role { get; set; }
}
