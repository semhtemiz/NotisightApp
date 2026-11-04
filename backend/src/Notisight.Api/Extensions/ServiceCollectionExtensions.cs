using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Notisight.Api.Features.AI.Services;
using Notisight.Api.Features.Auth.Services;
using Notisight.Api.Features.Ingestion.Services;
using Notisight.Api.Infrastructure.Auth;
using Notisight.Api.Infrastructure.Errors;
using Notisight.Api.Infrastructure.Persistence;
using Notisight.Api.Options;
using Notisight.Api.Features.AI.Contracts;
using Notisight.Api.Features.AI.Options;
using Notisight.Api.Features.Settings.Services;
using Notisight.Api.Features.Ingestion.Contracts;

namespace Notisight.Api.Extensions;

public static class ServiceCollectionExtensions
{
    private static readonly string[] DevelopmentCorsOrigins =
    [
        "http://localhost:3000",
        "http://localhost:5173"
    ];

    private const int MinimumSigningKeyLength = 32;

    private static readonly string[] PlaceholderSigningKeys =
    [
        "jwt_signing_key",
        "signing_key",
        "your_signing_key",
        "changeme",
        "secret"
    ];

    public static IServiceCollection AddNotisightApiServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddAppOptions(configuration);
        services.AddDatabase(configuration);
        services.AddJwtAuthentication(configuration);
        services.AddHttpContextAccessor();
        services.AddDataProtection();
        services.AddControllers();
        var allowedOrigins = GetAllowedCorsOrigins(configuration);
        if (allowedOrigins.Length == 0 && !environment.IsDevelopment())
        {
            throw new InvalidOperationException("Cors:AllowedOrigins must be configured outside Development.");
        }

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                if (allowedOrigins.Length > 0)
                {
                    builder.WithOrigins(allowedOrigins)
                           .AllowAnyMethod()
                           .AllowAnyHeader()
                           .AllowCredentials();
                    return;
                }

                if (environment.IsDevelopment())
                {
                    builder.SetIsOriginAllowed(IsDevelopmentCorsOrigin)
                           .AllowAnyMethod()
                           .AllowAnyHeader()
                           .AllowCredentials();
                    return;
                }

                builder.WithOrigins(DevelopmentCorsOrigins)
                       .AllowAnyMethod()
                       .AllowAnyHeader()
                       .AllowCredentials();
            });
        });
        // Azure App Service terminates TLS and proxies the request, so RemoteIpAddress is the
        // load balancer unless forwarded headers are applied first.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });

        services.AddEndpointsApiExplorer();
        services.AddHealthChecks();
        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddAuthorization(options =>
        {
            // Endpoints require authentication unless they explicitly opt out, so a forgotten
            // [Authorize] fails closed instead of exposing someone's notes.
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // The named policies must exist for the endpoint metadata even when the in-process
            // buckets are switched off, which integration tests and edge-limited hosts need.
            if (!configuration.GetValue("RateLimiting:Enabled", true))
            {
                options.AddPolicy("auth", httpContext => RateLimitPartition.GetNoLimiter("auth"));
                options.AddPolicy("ai", httpContext => RateLimitPartition.GetNoLimiter("ai"));
                return;
            }

            // Partitioned per client: a single global bucket let one caller lock every user out
            // of login while still allowing ~1.2M password guesses a day.
            options.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
                GetRateLimitPartitionKey(httpContext),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

            options.AddPolicy("ai", httpContext => RateLimitPartition.GetFixedWindowLimiter(
                GetRateLimitPartitionKey(httpContext),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
        });
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ILlmChatService, OpenAiChatService>();
        services.AddScoped<IChatConfigurationProvider, ChatConfigurationProvider>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISecurityService, SecurityService>();
        services.AddSingleton<IToneProfileService, ToneProfileService>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<ITextChunkingService, TextChunkingService>();
        // Redirects are disabled on every outbound client: a validated public base URL could
        // otherwise answer with a 302 straight at a cloud metadata endpoint.
        services.AddHttpClient<IEmbeddingService, GeminiEmbeddingService>()
            .ConfigurePrimaryHttpMessageHandler(NewNoRedirectHandler);
        services.AddHttpClient<ILlmChatService, OpenAiChatService>()
            .ConfigurePrimaryHttpMessageHandler(NewNoRedirectHandler);
        services.AddScoped<IChunkSearchService, ChunkSearchService>();
        services.AddMemoryCache();
        services.AddScoped<IIntentParserService, IntentParserService>();
        services.AddScoped<ISessionContextService, SessionContextService>();
        services.AddScoped<ISmartRetrievalService, SmartRetrievalService>();
        services.AddScoped<IConfidenceEngineService, ConfidenceEngineService>();
        services.AddScoped<IQueryOrchestratorService, QueryOrchestratorService>();
        services.AddScoped<IRagAnswerService, RagAnswerService>();
        services.AddScoped<IChatHistoryService, ChatHistoryService>();
        services.AddHttpClient<IQdrantVectorService, QdrantVectorService>()
            .ConfigurePrimaryHttpMessageHandler(NewNoRedirectHandler);
        services.AddScoped<INoteVectorSyncService, NoteVectorSyncService>();
        services.AddSingleton<IVectorSyncQueue, InMemoryVectorSyncQueue>();
        services.AddHostedService<VectorSyncWorker>();
        services.AddScoped<IPdfIngestionService, PdfIngestionService>();
        services.AddHttpClient<IAudioTranscriptionService, AudioTranscriptionService>()
            .ConfigurePrimaryHttpMessageHandler(NewNoRedirectHandler);
        services.AddScoped<IFileStorageService, CloudflareR2StorageService>();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Notisight API",
                Version = "v1",
                Description = "Core API for the Notisight personal knowledge assistant."
            });
            options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Bearer access token"
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = JwtBearerDefaults.AuthenticationScheme
                        }
                    },
                    []
                }
            });
        });

        return services;
    }

    private static string[] GetAllowedCorsOrigins(IConfiguration configuration)
    {
        var origins = configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>()?
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim().TrimEnd('/'))
            .Where(origin => !IsPlaceholderCorsOrigin(origin))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        if (origins.Length == 0)
        {
            var inlineOrigins = configuration["Cors:AllowedOrigins"];
            origins = string.IsNullOrWhiteSpace(inlineOrigins)
                ? []
                : inlineOrigins
                    .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(origin => origin.TrimEnd('/'))
                    .Where(origin => !IsPlaceholderCorsOrigin(origin))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
        }

        if (origins.Length > 0)
        {
            return origins;
        }

        return origins;
    }

    private static bool IsPlaceholderCorsOrigin(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Host.Equals("example.com", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.EndsWith(".example.com", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUsableSigningKey(string? signingKey) =>
        !string.IsNullOrWhiteSpace(signingKey) &&
        signingKey.Trim().Length >= MinimumSigningKeyLength &&
        !PlaceholderSigningKeys.Contains(signingKey.Trim(), StringComparer.OrdinalIgnoreCase);

    private static string GetRateLimitPartitionKey(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static HttpClientHandler NewNoRedirectHandler() => new() { AllowAutoRedirect = false };

    private static bool IsDevelopmentCorsOrigin(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        var host = uri.Host;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("::1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return System.Net.IPAddress.TryParse(host, out var ipAddress) && IsPrivateNetworkAddress(ipAddress);
    }

    private static bool IsPrivateNetworkAddress(System.Net.IPAddress ipAddress)
    {
        var bytes = ipAddress.GetAddressBytes();
        return bytes.Length == 4 &&
            (bytes[0] == 10 ||
             (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
             (bytes[0] == 192 && bytes[1] == 168));
    }

    private static IServiceCollection AddAppOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Issuer) &&
                    !string.IsNullOrWhiteSpace(options.Audience),
                "Jwt:Issuer and Jwt:Audience must be configured.")
            .Validate(
                options => IsUsableSigningKey(options.SigningKey),
                "Jwt:SigningKey must be at least 32 characters of random data and may not be a " +
                "placeholder value. Generate one with: openssl rand -base64 48")
            .ValidateOnStart();

        services.AddOptions<GeminiOptions>()
            .Bind(configuration.GetSection(GeminiOptions.SectionName));

        services.AddOptions<DeepgramOptions>()
            .Bind(configuration.GetSection(DeepgramOptions.SectionName));

        services.AddOptions<OpenAiOptions>()
            .Bind(configuration.GetSection(OpenAiOptions.SectionName));

        services.AddOptions<QdrantOptions>()
            .Bind(configuration.GetSection(QdrantOptions.SectionName));

        services.AddOptions<RagOptions>()
            .Bind(configuration.GetSection(RagOptions.SectionName));

        services.AddOptions<CloudflareR2Options>()
            .Bind(configuration.GetSection(CloudflareR2Options.SectionName));

        return services;
    }

    private static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            options.UseSqlServer(connectionString);
        });

        return services;
    }

    private static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });

        return services;
    }
}
