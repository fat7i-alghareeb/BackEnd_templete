using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Taxi.Application.Common.Interfaces;

using Testcontainers.PostgreSql;

using Xunit;

namespace Taxi.Api.IntegrationTests.Infrastructure;

/// <summary>
/// The real API (every middleware, filter, behaviour and interceptor) against a throw-away
/// PostgreSQL in Docker. One container is shared by the whole run (<see cref="ApiCollection"/>);
/// tests use unique data instead of resetting it. Migrations and seeding run at startup exactly as
/// they do outside Production.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@test.local";
    public const string AdminPassword = "Admin123!";
    public const string UserEmail = "user@test.local";
    public const string UserPassword = "User123!";

    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    private readonly string storageRoot = Path.Combine(Path.GetTempPath(), "taxi-it-" + Guid.NewGuid().ToString("N"));

    public CapturingEmailSender Emails { get; } = new();

    public async Task InitializeAsync() => await this.postgres.StartAsync();

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await this.postgres.DisposeAsync();

        if (Directory.Exists(this.storageRoot))
        {
            Directory.Delete(this.storageRoot, recursive: true);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:DefaultConnection", this.postgres.GetConnectionString());
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("JwtSettings:Secret", "integration-tests-secret-key-0123456789abcdef");
        builder.UseSetting("Seed:Admin:Email", AdminEmail);
        builder.UseSetting("Seed:Admin:Password", AdminPassword);
        builder.UseSetting("Seed:User:Email", UserEmail);
        builder.UseSetting("Seed:User:Password", UserPassword);
        builder.UseSetting("Seed:SampleData", "false");
        builder.UseSetting("FileStorage:LocalRootPath", this.storageRoot);

        // Tests drive the outbox processor themselves, deterministically.
        builder.UseSetting("Outbox:Enabled", "false");

        // Every test shares one client IP: keep the limiter out of the way except where a test
        // deliberately lowers it (RateLimitingTests).
        builder.UseSetting("RateLimiting:Default:PermitLimit", "100000");
        builder.UseSetting("RateLimiting:Auth:PermitLimit", "100000");

        // Quiet test output; Serilog's file sink is not wanted here.
        builder.UseSetting("Serilog:MinimumLevel:Default", "Warning");
        builder.UseSetting("Serilog:WriteTo:1:Name", "Console");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(this.Emails);
        });
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
