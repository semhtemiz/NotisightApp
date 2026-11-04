using Notisight.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNotisightApiServices(builder.Configuration, builder.Environment);

var app = builder.Build();

app.ApplyNotisightDatabaseMigrations();
app.UseNotisightApiPipeline();
app.MapControllers();

// The fallback authorization policy locks every endpoint by default; the platform health
// probe is the one endpoint that must stay reachable without a token.
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
