using System.Text;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using Taxi.Application.Common.Caching;
using Taxi.Application.Common.Interfaces;
using Taxi.Infrastructure.Data;
using Taxi.Infrastructure.Data.Interceptors;
using Taxi.Infrastructure.Email;
using Taxi.Infrastructure.HealthChecks;
using Taxi.Infrastructure.Identity;
using Taxi.Infrastructure.Settings;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AppSettings>(configuration.GetSection("AppSettings"));
        services.Configure<SeedSettings>(configuration.GetSection(SeedSettings.SectionName));
        services.AddSingleton(TimeProvider.System);

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is not configured. Set it through user secrets, " +
                "an environment variable (ConnectionStrings__DefaultConnection) or .env.");
        }

        // Order matters: soft delete turns Deleted into Modified first, so the audit interceptor
        // then stamps LastModified* on the same entry.
        services.AddScoped<ISaveChangesInterceptor, SoftDeleteInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseNpgsql(
                connectionString,
                npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: null);
                    npgsqlOptions.CommandTimeout(60);
                });
        });

        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        services.AddScoped<ApplicationDbContextInitialiser>();

        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer();

        // Configured from the validated JwtSettings rather than raw configuration, so the bearer
        // handler and TokenProvider can never disagree about issuer, audience or key.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>>((options, jwtSettings) =>
            {
                var jwt = jwtSettings.Value;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
                };
            });

        services
        .AddIdentityCore<AppUser>(options =>
        {
            options.Password.RequiredLength = 6;
            options.Password.RequireDigit = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
            options.Password.RequiredUniqueChars = 1;
            options.SignIn.RequireConfirmedAccount = false;
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        services.AddTransient<IIdentityService, IdentityService>();
        services.AddTransient<ITokenProvider, TokenProvider>();

        services.AddAppCaching(configuration);

        services.AddAppHealthChecks(configuration);

        services.AddAppEmail(configuration);

        return services;
    }

    /// <summary>
    /// HybridCache with an optional Redis L2. When <c>ConnectionStrings:Redis</c> is set, Redis is
    /// registered as the <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/>
    /// HybridCache uses as its second level, so every instance shares cached values and tag
    /// invalidations. Without it the cache is in-process only — fine for a single instance.
    /// <para>
    /// HybridCache has no backplane: after a <c>RemoveByTagAsync</c> on one node, another node may
    /// serve its own L1 copy for up to <c>LocalCacheExpiration</c> (30 s) before it re-checks L2.
    /// </para>
    /// </summary>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddAppCaching(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnectionString = configuration.GetConnectionString("Redis");

        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "taxi:";
            });
        }

        services.AddHybridCache(options => options.DefaultEntryOptions = new HybridCacheEntryOptions
        {
            Expiration = TimeSpan.FromMinutes(10),
            LocalCacheExpiration = CacheDefaults.LocalExpiration,
        });

        return services;
    }

    /// <summary>
    /// Readiness checks, all tagged <see cref="HealthCheckTags.Ready"/>: PostgreSQL through
    /// <see cref="AppDbContext"/> (<c>CanConnectAsync</c>), and Redis when it is configured.
    /// The API maps them to <c>/health/ready</c>; <c>/health/live</c> runs none of them.
    /// </summary>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddAppHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var checks = services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("postgres", tags: [HealthCheckTags.Ready]);

        if (!string.IsNullOrWhiteSpace(configuration.GetConnectionString("Redis")))
        {
            checks.AddCheck<DistributedCacheHealthCheck>("redis", tags: [HealthCheckTags.Ready]);
        }

        // A probe must answer quickly. Without a timeout the PostgreSQL check inherits the
        // DbContext's EnableRetryOnFailure policy and takes ~20 s to report a database outage.
        services.Configure<HealthCheckServiceOptions>(options =>
        {
            foreach (var registration in options.Registrations)
            {
                registration.Timeout = TimeSpan.FromSeconds(5);
            }
        });

        return services;
    }

    /// <summary>
    /// <see cref="IEmailSender"/> chosen by <c>Email:Provider</c>: <c>Log</c> (default) writes the
    /// message to the log; <c>Smtp</c> sends through <c>Email:Smtp</c> with MailKit.
    /// </summary>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddAppEmail(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EmailSettings>()
            .Bind(configuration.GetSection(EmailSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var provider = configuration[$"{EmailSettings.SectionName}:Provider"];

        if (string.Equals(provider, EmailProviders.Smtp, StringComparison.OrdinalIgnoreCase))
        {
            services.AddTransient<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            services.AddTransient<IEmailSender, LoggingEmailSender>();
        }

        return services;
    }
}
