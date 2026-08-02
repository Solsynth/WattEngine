using DysonNetwork.Shared.Auth;
using WattEngine.Valve.Workspace;

namespace WattEngine.Valve.Startup;

public static class ApplicationBuilderExtensions
{
    extension(WebApplication app)
    {
        public WebApplication ConfigureAppMiddleware()
        {
            app.UseAuthentication();
            app.UseDyAuthModelProjection();
            app.UseMiddleware<RemotePermissionMiddleware>();
            app.UseAuthorization();
            app.MapControllers();

            return app;
        }

        public WebApplication ConfigureGrpcServices()
        {
            app.MapGrpcService<WorkspaceGrpcService>();
            app.MapGrpcReflectionService();

            return app;
        }
    }
}
