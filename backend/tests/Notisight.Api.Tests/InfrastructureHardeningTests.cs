using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Notisight.Api.Tests;

public sealed class InfrastructureHardeningTests
{
    private const string ProductionOrigin = "https://app.notisight.dev";

    [Fact]
    public void Production_Requires_Configured_Cors_Origins()
    {
        using var factory = CreateProductionFactory();

        var exception = Assert.ThrowsAny<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("Cors:AllowedOrigins", exception.ToString());
    }

    [Fact]
    public async Task Production_Cors_Only_Allows_Configured_Origin()
    {
        using var factory = CreateProductionFactory(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = ProductionOrigin
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var allowedResponse = await client.SendAsync(CreatePreflightRequest(ProductionOrigin));
        Assert.Equal(HttpStatusCode.NoContent, allowedResponse.StatusCode);
        Assert.True(allowedResponse.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowedOrigins));
        Assert.Equal(ProductionOrigin, Assert.Single(allowedOrigins));

        var rejectedResponse = await client.SendAsync(CreatePreflightRequest("http://localhost:3000"));
        Assert.False(rejectedResponse.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Production_Health_Response_Includes_Security_Headers()
    {
        using var factory = CreateProductionFactory(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = ProductionOrigin
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertHeader(response, "X-Content-Type-Options", "nosniff");
        AssertHeader(response, "X-Frame-Options", "DENY");
        AssertHeader(response, "Referrer-Policy", "no-referrer");
        AssertHeader(response, "Content-Security-Policy", "frame-ancestors 'none'");
        Assert.True(response.Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public async Task Production_Skips_Startup_Migrations_ByDefault_And_Runs_When_Opted_In()
    {
        using var defaultFactory = CreateProductionFactory(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = ProductionOrigin
        });
        using var defaultClient = defaultFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        var healthResponse = await defaultClient.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);

        using var optInFactory = CreateProductionFactory(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = ProductionOrigin,
            ["Database:ApplyMigrationsOnStartup"] = "true"
        });

        var exception = Assert.ThrowsAny<Exception>(() => optInFactory.CreateClient());
        Assert.Contains("ConnectionString", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static WebApplicationFactory<Program> CreateProductionFactory(
        Dictionary<string, string?>? overrides = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "notisight-tests",
            ["Jwt:Audience"] = "notisight-tests",
            ["Jwt:SigningKey"] = "test-signing-key-with-enough-length-for-hmac",
            ["Jwt:AccessTokenMinutes"] = "60",
            ["Jwt:RefreshTokenDays"] = "14"
        };

        if (overrides is not null)
        {
            foreach (var (key, value) in overrides)
            {
                settings[key] = value;
            }
        }

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            foreach (var (key, value) in settings)
            {
                if (value is not null)
                {
                    builder.UseSetting(key, value);
                }
            }

            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(settings);
            });
        });
    }

    private static HttpRequestMessage CreatePreflightRequest(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/health");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        return request;
    }

    private static void AssertHeader(HttpResponseMessage response, string name, string expectedValue)
    {
        Assert.True(response.Headers.TryGetValues(name, out var values), $"Missing {name} header.");
        Assert.Equal(expectedValue, Assert.Single(values));
    }
}
