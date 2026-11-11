using Scalar.AspNetCore;

namespace AppTemplate.Web.Configurations;

public static class MiddlewareConfigurations
{
    public static async Task<IApplicationBuilder> UseAppMiddleware(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            // FastEndpoints' handler: logs the exception, and answers 500 without its message.
            app.UseDefaultExceptionHandler(useGenericReason: true);
            app.UseHsts();
        }

        app.UseAuthentication().UseAuthorization();

        app.UseFastEndpoints();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwaggerGen(
                options =>
                {
                    options.Path = "/openapi/{documentName}.json";
                },
                settings =>
                {
                    settings.Path = "/swagger";
                    settings.DocumentPath = "/openapi/{documentName}.json";
                }
            );

            app.MapScalarApiReference(options =>
                {
                    options.WithTitle("AppTemplate API");
                    options.WithOpenApiRoutePattern("/openapi/{documentName}.json");
                })
                .AllowAnonymous(); // a browser page, open before signing in
        }

        app.UseHttpsRedirection();

        return app;
    }
}
