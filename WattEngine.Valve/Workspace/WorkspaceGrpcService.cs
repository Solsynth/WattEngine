using DysonNetwork.Shared.Models;
using DysonNetwork.Shared.Proto;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace WattEngine.Valve.Workspace;

public class WorkspaceGrpcService(
    WorkspaceService workspaces,
    PermissionService permissions
) : DyWorkspaceService.DyWorkspaceServiceBase
{
    private static Guid ParseId(string value, string fieldName)
    {
        if (!Guid.TryParse(value, out var id))
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"{fieldName} must be a GUID."));
        return id;
    }

    private static DyWorkspace ToProto(WtWorkspace workspace)
    {
        var result = new DyWorkspace
        {
            Id = workspace.Id.ToString(),
            Slug = workspace.Slug,
            Name = workspace.Name,
            Description = workspace.Description ?? string.Empty,
            Type = (DyWorkspaceType)workspace.Type,
            OwnerAccountId = workspace.OwnerAccountId.ToString(),
            Plan = (DyWorkspacePlan)workspace.Plan
        };

        if (workspace.Picture is not null) result.Picture = workspace.Picture.ToProtoValue();
        if (workspace.Background is not null) result.Background = workspace.Background.ToProtoValue();
        if (workspace.PlanExpiresAt.HasValue)
            result.PlanExpiresAt = Timestamp.FromDateTime(workspace.PlanExpiresAt.Value.ToDateTimeUtc());

        return result;
    }

    private static DyWorkspaceMember ToProto(WtWorkspaceMember member)
    {
        var result = new DyWorkspaceMember
        {
            Id = member.Id.ToString(),
            WorkspaceId = member.WorkspaceId.ToString(),
            AccountId = member.AccountId.ToString(),
            Role = member.Role
        };

        if (member.JoinedAt.HasValue)
            result.JoinedAt = Timestamp.FromDateTime(member.JoinedAt.Value.ToDateTimeUtc());
        if (member.LeaveAt.HasValue)
            result.LeaveAt = Timestamp.FromDateTime(member.LeaveAt.Value.ToDateTimeUtc());

        return result;
    }

    public override async Task<DyWorkspace> GetWorkspace(DyGetWorkspaceRequest request, ServerCallContext context)
    {
        var workspace = request.QueryCase switch
        {
            DyGetWorkspaceRequest.QueryOneofCase.Id => await workspaces.GetById(ParseId(request.Id, "id")),
            DyGetWorkspaceRequest.QueryOneofCase.Slug when !string.IsNullOrWhiteSpace(request.Slug) => await workspaces.GetBySlug(request.Slug),
            _ => throw new RpcException(new Status(StatusCode.InvalidArgument, "Must provide either id or slug."))
        };

        if (workspace is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Workspace not found."));

        return ToProto(workspace);
    }

    public override async Task<DyGetWorkspaceBatchResponse> GetWorkspaceBatch(
        DyGetWorkspaceBatchRequest request, ServerCallContext context)
    {
        var response = new DyGetWorkspaceBatchResponse();
        foreach (var id in request.Ids.Distinct())
        {
            var workspace = await workspaces.GetById(ParseId(id, "ids"));
            if (workspace is not null) response.Workspaces.Add(ToProto(workspace));
        }
        return response;
    }

    public override async Task<DyGetUserWorkspacesResponse> GetUserWorkspaces(
        DyGetUserWorkspacesRequest request, ServerCallContext context)
    {
        var userWorkspaces = await workspaces.GetUserWorkspaces(ParseId(request.AccountId, "account_id"));
        var response = new DyGetUserWorkspacesResponse();
        response.WorkspaceIds.AddRange(userWorkspaces.Select(workspace => workspace.Id.ToString()));
        return response;
    }

    public override async Task<BoolValue> IsMemberWithRole(
        DyIsWorkspaceMemberWithRoleRequest request, ServerCallContext context)
    {
        if (request.RequiredRoles.Count == 0) return new BoolValue { Value = false };

        var isMember = await workspaces.IsMemberWithRole(
            ParseId(request.WorkspaceId, "workspace_id"),
            ParseId(request.AccountId, "account_id"),
            request.RequiredRoles.Max());
        return new BoolValue { Value = isMember };
    }

    public override async Task<BoolValue> HasPermission(
        DyHasWorkspacePermissionRequest request, ServerCallContext context)
    {
        var hasPermission = await permissions.HasPermission(
            ParseId(request.WorkspaceId, "workspace_id"),
            ParseId(request.AccountId, "account_id"),
            request.Permission);
        return new BoolValue { Value = hasPermission };
    }

    public override Task<DyWorkspacePlanQuota> GetPlanQuota(DyGetPlanQuotaRequest request, ServerCallContext context)
    {
        var plan = (WorkspacePlan)request.Plan;
        return Task.FromResult(new DyWorkspacePlanQuota
        {
            Plan = request.Plan,
            MaxProjects = WorkspacePlanQuota.GetMaxProjects(plan),
            MaxMembersPerWorkspace = WorkspacePlanQuota.GetMaxMembers(plan),
            MaxTasksPerProject = WorkspacePlanQuota.GetMaxTasksPerProject(plan),
            MaxBroadsPerProject = WorkspacePlanQuota.GetMaxBroadsPerProject(plan),
            MaxStorageBytes = WorkspacePlanQuota.GetMaxStorageBytes(plan)
        });
    }

    public override async Task<DyWorkspaceMember> LoadMemberAccount(
        DyLoadWorkspaceMemberRequest request, ServerCallContext context)
    {
        var requested = request.Member;
        var member = await workspaces.GetMember(
            ParseId(requested.WorkspaceId, "member.workspace_id"),
            ParseId(requested.AccountId, "member.account_id"));
        return member is null ? requested : ToProto(member);
    }

    public override async Task<DyLoadWorkspaceMembersResponse> LoadMemberAccounts(
        DyLoadWorkspaceMembersRequest request, ServerCallContext context)
    {
        var response = new DyLoadWorkspaceMembersResponse();
        foreach (var requested in request.Members)
        {
            var member = await workspaces.GetMember(
                ParseId(requested.WorkspaceId, "member.workspace_id"),
                ParseId(requested.AccountId, "member.account_id"));
            response.Members.Add(member is null ? requested : ToProto(member));
        }
        return response;
    }
}
