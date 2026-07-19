using Scalar.AspNetCore;

using Serilog;

using Taxi.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services
    .AddPresentation(builder.Configuration, builder.Environment)
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

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
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Taxi API V1");
        options.EnableDeepLinking();
        options.DisplayRequestDuration();
        options.EnableFilter();
    });

    app.MapScalarApiReference();
}
else
{
    app.UseHsts();
}

app.UseCoreMiddlewares(builder.Configuration);

app.MapControllers();

app.Run();

public partial class Program;