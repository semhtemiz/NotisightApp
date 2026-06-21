using Notisight.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNotisightApiServices(builder.Configuration, builder.Environment);

var app = builder.Build();

app.ApplyNotisightDatabaseMigrations();
app.UseNotisightApiPipeline();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
