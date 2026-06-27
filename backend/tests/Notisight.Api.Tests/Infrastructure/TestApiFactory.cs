using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Notisight.Api.Features.AI.Contracts;
using Notisight.Api.Features.AI.Services;
using Notisight.Api.Features.Ingestion.Contracts;
using Notisight.Api.Features.Ingestion.Services;
using Notisight.Api.Infrastructure.Persistence;
using Notisight.Api.Options;

namespace Notisight.Api.Tests.Infrastructure;

public sealed class TestApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _databaseName = $"notisight-tests-{Guid.NewGuid():N}";

    public RecordingQdrantVectorService VectorStore { get; } = new();
    public RecordingAudioTranscriptionService AudioTranscription { get; } = new();
    public RecordingFileStorageService FileStorage { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "notisight-tests",
                ["Jwt:Audience"] = "notisight-tests",
                ["Jwt:SigningKey"] = "test-signing-key-with-enough-length-for-hmac",
                ["Jwt:AccessTokenMinutes"] = "60",
                ["Jwt:RefreshTokenDays"] = "14",
                ["Database:SkipMigrations"] = "true"
            });
        });
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

            if (dbContextDescriptor is not null)
            {
                services.Remove(dbContextDescriptor);
            }

            services.RemoveAll<IQdrantVectorService>();
            services.AddSingleton<IQdrantVectorService>(VectorStore);
            services.RemoveAll<IEmbeddingService>();
            services.AddSingleton<IEmbeddingService, DeterministicEmbeddingService>();
            services.RemoveAll<ILlmChatService>();
            services.AddSingleton<ILlmChatService, DeterministicLlmChatService>();
            services.RemoveAll<IAudioTranscriptionService>();
            services.AddSingleton<IAudioTranscriptionService>(AudioTranscription);
            services.RemoveAll<IFileStorageService>();
            services.AddSingleton<IFileStorageService>(FileStorage);
            services.Configure<JwtOptions>(options =>
            {
                options.Issuer = "notisight-tests";
                options.Audience = "notisight-tests";
                options.SigningKey = "test-signing-key-with-enough-length-for-hmac";
                options.AccessTokenMinutes = 60;
                options.RefreshTokenDays = 14;
            });
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters.ValidIssuer = "notisight-tests";
                options.TokenValidationParameters.ValidAudience = "notisight-tests";
                options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes("test-signing-key-with-enough-length-for-hmac"));
            });

            services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });

            var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Database.EnsureCreated();
        });
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public new Task DisposeAsync() => Task.CompletedTask;
}
