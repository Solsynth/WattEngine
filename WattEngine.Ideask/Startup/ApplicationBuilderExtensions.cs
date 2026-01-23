namespace WattEngine.Ideask.Startup;

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
            // Map your gRPC services here
            app.MapGrpcReflectionService();

            return app;
        }
    }
}
