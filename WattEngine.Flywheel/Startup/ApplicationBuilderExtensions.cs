using DysonNetwork.Shared.Auth;
using Microsoft.AspNetCore.Diagnostics;
using WattEngine.Flywheel.Flywheel;

namespace WattEngine.Flywheel.Startup;

public static class ApplicationBuilderExtensions
{
    extension(WebApplication app)
    {
        public WebApplication ConfigureAppMiddleware()
        {
            app.UseExceptionHandler(handler => handler.Run(context =>
            {
                var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
                context.Response.StatusCode = exception switch
                {
                    FlywheelValidationException => StatusCodes.Status400BadRequest,
                    FlywheelForbiddenException => StatusCodes.Status403Forbidden,
                    FlywheelNotFoundException => StatusCodes.Status404NotFound,
                    FlywheelConflictException => StatusCodes.Status409Conflict,
                    _ => StatusCodes.Status500InternalServerError
                };
                return context.Response.WriteAsJsonAsync(new { error = exception?.Message ?? "Unexpected server error." });
            }));
            app.UseAuthentication();
            app.UseDyAuthModelProjection();
            app.UseAuthorization();
            app.MapControllers();
            return app;
        }
    }
}
