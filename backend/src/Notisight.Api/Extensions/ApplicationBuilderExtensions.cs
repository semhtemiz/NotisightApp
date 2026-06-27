using Microsoft.EntityFrameworkCore;
using Notisight.Api.Infrastructure.Persistence;

namespace Notisight.Api.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseNotisightApiPipeline(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseExceptionHandler();
        app.UseNotisightSecurityHeaders(app.Environment);
        app.UseCors();
        app.UseHttpsRedirection();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }

    public static WebApplication ApplyNotisightDatabaseMigrations(this WebApplication app)
    {
        if (app.Configuration.GetValue<bool>("Database:SkipMigrations"))
        {
            return app;
        }

        var shouldApplyMigrations = app.Environment.IsDevelopment() ||
            app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup");

        if (!shouldApplyMigrations)
        {
            return app;
        }

        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.Migrate();

        return app;
    }

    private static IApplicationBuilder UseNotisightSecurityHeaders(this IApplicationBuilder app, IHostEnvironment environment)
    {
        return app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            if (!environment.IsDevelopment())
            {
                headers.TryAdd("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
            }

            headers.TryAdd("X-Content-Type-Options", "nosniff");
            headers.TryAdd("X-Frame-Options", "DENY");
            headers.TryAdd("Referrer-Policy", "no-referrer");
            headers.TryAdd("Permissions-Policy", "camera=(), geolocation=(), payment=()");
            headers.TryAdd("Content-Security-Policy", "frame-ancestors 'none'");

            await next();
        });
    }
}
