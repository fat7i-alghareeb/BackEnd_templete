using System.Globalization;
using System.Net;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

using Asp.Versioning;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

using Serilog;

using Taxi.Api;
using Taxi.Api.Infrastructure;
using Taxi.Api.OpenApi.Transformers;
using Taxi.Api.Services;
using Taxi.Application.Common.Interfaces;
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
                .AddAppOutputCaching()
                .AddAppLocalization()
                .AddConfiguredCors(configuration)
                .AddAppRateLimiting()
                .AddAppForwardedHeaders(configuration, environment)
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

    public static IServiceCollection AddAppLocalization(this IServiceCollection services)
    {
        services.AddJsonLocalization(options => options.ResourcesPath = "Resources");
        services.AddLocalization();
        return services;
    }

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.AddSlidingWindowLimiter("SlidingWindow", limiterOptions =>
            {
                limiterOptions.PermitLimit = 100;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.SegmentsPerWindow = 6;
                limiterOptions.QueueLimit = 10;
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.AutoReplenishment = true;
            });

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });

        return services;
    }

    public static IServiceCollection AddAppOutputCaching(this IServiceCollection services)
    {
        services.AddOutputCache(options =>
        {
            options.SizeLimit = 100 * 1024 * 1024; // 100 mb
            options.AddBasePolicy(policy => policy
                .Expire(TimeSpan.FromSeconds(60))
                .SetVaryByHeader("Accept-Language"));
        });

        return services;
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

        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseSerilogRequestLogging();
        app.UseCors(configuration["AppSettings:CorsPolicyName"]!);
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseOutputCache();

        return app;
    }
}