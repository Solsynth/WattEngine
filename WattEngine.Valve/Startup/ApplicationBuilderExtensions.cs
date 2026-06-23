using WattEngine.Valve.Workspace;

namespace WattEngine.Valve.Startup;

public static class ApplicationBuilderExtensions
{
    extension(WebApplication app)
    {
        public WebApplication ConfigureAppMiddleware()
        {
            app.UseAuthorization();
            app.MapControllers();

            return app;
        }

        public WebApplication ConfigureGrpcServices()
        {
            // app.MapGrpcService<WorkspaceGrpcService>(); // Add when proto is defined
            app.MapGrpcReflectionService();

            return app;
        }
    }
}
