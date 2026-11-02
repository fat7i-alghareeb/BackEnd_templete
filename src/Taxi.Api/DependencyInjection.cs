using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

using Asp.Versioning;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Serilog;

using Taxi.Api;
using Taxi.Api.Infrastructure;
using Taxi.Api.Infrastructure.RateLimiting;
using Taxi.Api.OpenApi.Transformers;
using Taxi.Api.Services;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Common.Security;
using Taxi.Contracts.Common;
using Taxi.Infrastructure.Data;
using Taxi.Infrastructure.Settings;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddCustomProblemDetails()
                .AddCustomApiVersioning()
                .AddExceptionHandling()
                .AddControllerWithJsonConfiguration()
                .AddValidation()
                .AddIdentityInfrastructure()
                .AddAppAuthorization()
                .AddAppLocalization()
                .AddConfiguredCors(configuration)
                .AddAppRateLimiting()
                .AddAppForwardedHeaders(configuration, environment)
                .AddAppSecurityHardening()
                .AddObservability()
                .AddApiDocumentation();

        return services;
    }

    public static IServiceCollection AddAppForwardedHeaders(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var settings = configuration.GetSection("ForwardedHeaders").Get<ForwardedHeadersSettings>()
            ?? new ForwardedHeadersSettings();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = settings.ForwardedHeaders;

            if (settings.ForwardLimit.HasValue)
            {
                options.ForwardLimit = settings.ForwardLimit;
            }

            if (settings.KnownProxies is { Length: > 0 })
            {
                foreach (var proxy in settings.KnownProxies)
                {
                    try
                    {
                        options.KnownProxies.Add(IPAddress.Parse(proxy));
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"Invalid ForwardedHeaders:KnownProxies value '{proxy}'.",
                            ex);
                    }
                }
            }

            if (settings.KnownIPNetworks is { Length: > 0 })
            {
                foreach (var network in settings.KnownIPNetworks)
                {
                    try
                    {
                        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"Invalid ForwardedHeaders:KnownIPNetworks value '{network}'.",
                            ex);
                    }
                }
            }

            if (environment.IsDevelopment() && settings.AllowAllInDevelopment)
            {
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
                options.ForwardLimit = null;
            }
        });

        return services;
    }

    /// <summary>
    /// HSTS settings for <c>UseHsts()</c> (outside Development). Request body size is a Kestrel
    /// limit bound from <c>Kestrel:Limits:MaxRequestBodySize</c> in configuration; endpoints that
    /// accept files raise it locally with <c>[RequestSizeLimit]</c>.
    /// </summary>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddAppSecurityHardening(this IServiceCollection services)
    {
        services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(365);
            options.IncludeSubDomains = true;
        });

        return services;
    }

    public static IServiceCollection AddAppLocalization(this IServiceCollection services)
    {
        services.AddJsonLocalization(options => options.ResourcesPath = "Resources");
        services.AddLocalization();
        return services;
    }

    /// <summary>
    /// Registers the two named policies in <see cref="RateLimitPolicies"/>. Limits come from the
    /// <c>RateLimiting</c> section through <see cref="RateLimitingSettings"/> and are read when the
    /// limiter is first built, so test hosts can override them.
    /// <para>
    /// IP partitioning relies on <c>UseForwardedHeaders</c> resolving the real client address — behind
    /// a proxy that is not listed in <c>ForwardedHeaders:KnownProxies</c>, every caller shares one bucket.
    /// </para>
    /// </summary>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RateLimitingSettings>()
            .BindConfiguration(RateLimitingSettings.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(_ => { });

        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RateLimitingSettings>>((options, settingsAccessor) =>
            {
                var settings = settingsAccessor.Value;

                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.AddPolicy(RateLimitPolicies.Default, httpContext =>
                    RateLimitPartition.GetSlidingWindowLimiter(
                        partitionKey: httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                            ?? ClientIp(httpContext),
                        factory: _ => new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = settings.Default.PermitLimit,
                            Window = TimeSpan.FromSeconds(settings.Default.WindowSeconds),
                            SegmentsPerWindow = settings.Default.SegmentsPerWindow,
                            QueueLimit = settings.Default.QueueLimit,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            AutoReplenishment = true,
                        }));

                // Credential endpoints are anonymous, so the only usable partition is the caller's IP.
                // No queue: a brute-force attempt should be rejected, not delayed.
                options.AddPolicy(RateLimitPolicies.Auth, httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: ClientIp(httpContext),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = settings.Auth.PermitLimit,
                            Window = TimeSpan.FromSeconds(settings.Auth.WindowSeconds),
                            QueueLimit = 0,
                            AutoReplenishment = true,
                        }));

                options.OnRejected = WriteRateLimitProblemAsync;
            });

        return services;

        static string ClientIp(HttpContext httpContext) =>
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    public static IServiceCollection AddCustomProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = (context) =>
        {
            context.ProblemDetails.Instance = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
            context.ProblemDetails.Extensions.Add("requestId", context.HttpContext.TraceIdentifier);
        });

        return services;
    }

    public static IServiceCollection AddCustomApiVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        }).AddMvc()
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        return services;
    }

    /// <summary>
    /// Traces and metrics via OpenTelemetry. The OTLP exporter reads its endpoint and protocol
    /// from the standard OTEL_EXPORTER_OTLP_* environment variables, which docker-compose already
    /// sets to the Seq ingest endpoint. Metrics are exposed for Prometheus by
    /// MapPrometheusScrapingEndpoint in Program.cs.
    /// </summary>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddObservability(this IServiceCollection services)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: "Taxi.Api",
                serviceVersion: typeof(IAssemblyMarker).Assembly.GetName().Version?.ToString()))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddPrometheusExporter());

        return services;
    }

    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        string[] versions = ["v1"];

        foreach (var version in versions)
        {
            services.AddOpenApi(version, options =>
            {
                // Versioning config
                options.AddDocumentTransformer<VersionInfoTransformer>();

                // Security Scheme config
                options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();

                // Security Operation config
                options.AddOperationTransformer<BearerSecurityOperationTransformer>();

                // Localization Header config
                options.AddOperationTransformer<AcceptLanguageOperationTransformer>();
            });
        }

        return services;
    }

    public static IServiceCollection AddExceptionHandling(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        return services;
    }

    public static IServiceCollection AddControllerWithJsonConfiguration(this IServiceCollection services)
    {
        services.AddControllers().AddJsonOptions(options => options
            .JsonSerializerOptions
            .DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

        return services;
    }

    public static IServiceCollection AddValidation(this IServiceCollection services)
    {
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var sp = context.HttpContext.RequestServices;
                var localizer = sp.GetRequiredService<IStringLocalizer<SharedResource>>();
                var logger = sp.GetRequiredService<ILogger<SharedResource>>();
                var env = sp.GetRequiredService<IHostEnvironment>();

                var problemDetails = new ValidationProblemDetails(
                    context.ModelState.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value!.Errors
                            .Select(error =>
                            {
                                var key = error.ErrorMessage;
                                var localized = localizer[key];
                                if (!localized.ResourceNotFound)
                                {
                                    return localized.Value;
                                }

                                if (env.IsDevelopment())
                                {
                                    logger.LogWarning(
                                        "Missing localization key '{Key}' for culture '{Culture}'",
                                        key,
                                        CultureInfo.CurrentUICulture.Name);
                                }

                                return key;
                            })
                            .ToArray()))
                {
                    Status = StatusCodes.Status400BadRequest,
                };

                return new BadRequestObjectResult(problemDetails);
            };
        });

        return services;
    }

    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<ILanguageContext, LanguageContext>();
        services.AddScoped<IUser, CurrentUser>();
        services.AddHttpContextAccessor();

        return services;
    }

    /// <summary>
    /// Maps every policy in <see cref="Policies"/> to the roles that hold it, and sets a fallback
    /// policy so an endpoint without any <c>[Authorize]</c> / <c>[AllowAnonymous]</c> metadata
    /// requires an authenticated user instead of being silently public.
    /// </summary>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddAppAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.CanReadCars, policy => policy.RequireRole(Roles.Manager, Roles.User))
            .AddPolicy(Policies.CanManageCars, policy => policy.RequireRole(Roles.Manager));

        return services;
    }

    public static IServiceCollection AddConfiguredCors(this IServiceCollection services, IConfiguration configuration)
    {
        var appSettings = configuration.GetSection("AppSettings").Get<AppSettings>()!;

        services.AddCors(options => options.AddPolicy(
            appSettings.CorsPolicyName,
            policy => policy
                .WithOrigins(appSettings.AllowedOrigins!)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()));

        return services;
    }

    public static async Task ApplyMigrationsWithRetryAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        // Opt-in destructive reset — only honored in Development to prevent
        // accidental data loss in production even if the flag is misconfigured.
        var resetOnStartup = configuration.GetValue<bool>("Database:ResetOnStartup");
        if (resetOnStartup && app.Environment.IsDevelopment())
        {
            logger.LogWarning("Database:ResetOnStartup=true — dropping and recreating database.");
            await initialiser.ResetDatabaseAsync();
        }
        else if (resetOnStartup)
        {
            logger.LogWarning("Database:ResetOnStartup=true ignored — only allowed in Development environment.");
        }

        const int maxAttempts = 5;
        var delay = TimeSpan.FromSeconds(3);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                logger.LogInformation("Applying database migrations (attempt {Attempt}/{MaxAttempts}).", attempt, maxAttempts);
                await initialiser.InitialiseAsync();
                await initialiser.SeedAsync();
                logger.LogInformation("Database migrations completed successfully.");
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(ex, "Database migration attempt {Attempt} failed. Retrying in {DelaySeconds}s.", attempt, delay.TotalSeconds);
                await Task.Delay(delay);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Database migration failed after {MaxAttempts} attempts.", maxAttempts);
                throw;
            }
        }
    }

    public static IApplicationBuilder UseCoreMiddlewares(this IApplicationBuilder app, IConfiguration configuration)
    {
        app.UseRequestLocalization(new RequestLocalizationOptions()
            .SetDefaultCulture(Languages.Default)
            .AddSupportedCultures(Languages.All)
            .AddSupportedUICultures(Languages.All));

        app.UseMiddleware<RequestLogContextMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();

        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseSerilogRequestLogging();
        app.UseCors(configuration["AppSettings:CorsPolicyName"]!);
        app.UseAuthentication();

        // After authentication so RateLimitPolicies.Default can partition by user id; before
        // authorization and the endpoint so anonymous credential endpoints are still throttled.
        app.UseRateLimiter();
        app.UseAuthorization();

        return app;
    }

    private static async ValueTask WriteRateLimitProblemAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            httpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        var localizer = httpContext.RequestServices.GetRequiredService<IStringLocalizer<SharedResource>>();
        var problemDetailsService = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();

        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = localizer[LocalizationKeys.RateLimit.Exceeded].Value,
            },
        });
    }
}
