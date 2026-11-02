using Scalar.AspNetCore;

using Serilog;

using Taxi.Api.Infrastructure.HealthChecks;
using Taxi.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services
    .AddPresentation(builder.Configuration, builder.Environment)
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.WebHost.ConfigureKestrel((context, options) =>
{
    // The Server header only advertises the stack to an attacker.
    options.AddServerHeader = false;

    // Default 1 MB: every request here is a small JSON document. Endpoints that accept files raise
    // it locally with [RequestSizeLimit]. Kestrel does not bind its Limits from configuration by
    // itself, so the key is read explicitly.
    options.Limits.MaxRequestBodySize =
        context.Configuration.GetValue<long?>("Kestrel:Limits:MaxRequestBodySize") ?? 1_048_576;
});

builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig.ReadFrom.Configuration(context.Configuration));

var app = builder.Build();

var applyMigrationsOnStartup =
    builder.Configuration.GetValue<bool?>("Database:ApplyMigrationsOnStartup")
    ?? !app.Environment.IsProduction();
if (applyMigrationsOnStartup)
{
    await app.ApplyMigrationsWithRetryAsync();
}
else
{
    app.Logger.LogInformation(
        "Database migrations are disabled at API startup; deployment must apply migrations before traffic is enabled.");
}

app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Taxi API V1");
        options.EnableDeepLinking();
        options.DisplayRequestDuration();
        options.EnableFilter();
    });

    app.MapScalarApiReference().AllowAnonymous();
}
else
{
    app.UseHsts();
}

app.UseCoreMiddlewares(builder.Configuration);

// Scraped by the prometheus service at /metrics (see containers/prometheus/prometheus.yml).
// Intentionally unauthenticated: it is only reachable on the published container port.
app.MapPrometheusScrapingEndpoint().AllowAnonymous();

app.MapAppHealthChecks();

app.MapControllers();

app.Run();

public partial class Program;