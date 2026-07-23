using DysonNetwork.Shared.Auth;

namespace WattEngine.Ideask.Startup;

public static class ApplicationBuilderExtensions
{
    extension(WebApplication app)
    {
        public WebApplication ConfigureAppMiddleware()
        {
            app.UseAuthentication();
            app.UseDyAuthModelProjection();
            app.UseAuthorization();
            app.MapControllers();

            return app;
        }

        public WebApplication ConfigureGrpcServices()
        {
            // Map your gRPC services here
            app.MapGrpcReflectionService();

            return app;
        }
    }
}
