using System.Data.Common;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Notisight.Api.Features.AI.Contracts;
using Notisight.Api.Features.AI.Services;
using Notisight.Api.Features.Ingestion.Services;
using Notisight.Api.Infrastructure.Persistence;
using Notisight.Api.Options;

namespace Notisight.Api.Tests.Infrastructure;

public sealed class TestApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private DbConnection _connection = null!;

    public RecordingQdrantVectorService VectorStore { get; } = new();
    public RecordingAudioTranscriptionService AudioTranscription { get; } = new();

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
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        builder.ConfigureServices(services =>
        {
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

            if (dbContextDescriptor is not null)
            {
                services.Remove(dbContextDescriptor);
            }

            var connectionDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbConnection));

            if (connectionDescriptor is not null)
            {
                services.Remove(connectionDescriptor);
            }

            services.AddSingleton(_connection);

            services.RemoveAll<IQdrantVectorService>();
            services.AddSingleton<IQdrantVectorService>(VectorStore);
            services.RemoveAll<IEmbeddingService>();
            services.AddSingleton<IEmbeddingService, DeterministicEmbeddingService>();
            services.RemoveAll<ILlmChatService>();
            services.AddSingleton<ILlmChatService, DeterministicLlmChatService>();
            services.RemoveAll<IAudioTranscriptionService>();
            services.AddSingleton<IAudioTranscriptionService>(AudioTranscription);
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
                var connection = serviceProvider.GetRequiredService<DbConnection>();
                options.UseSqlite(connection);
            });

            var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Database.EnsureCreated();
        });
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public new async Task DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}
