using System.Globalization;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;

using Taxi.Domain.Common.Results;

namespace Taxi.Api.Extensions;

/// <summary>
/// Single canonical path for translating <see cref="Error"/> values into
/// RFC 7807 ProblemDetails / ValidationProblemDetails responses.
/// </summary>
public static class ProblemExtensions
{
    public static IActionResult ToProblem(this List<Error> errors, ControllerBase controller)
    {
        if (errors.Count == 0)
        {
            return controller.Problem();
        }

        var sp = controller.HttpContext.RequestServices;
        var localizer = sp.GetRequiredService<IStringLocalizer<SharedResource>>();
        var logger = sp.GetRequiredService<ILogger<SharedResource>>();
        var env = sp.GetRequiredService<IHostEnvironment>();

        if (errors.TrueForAll(e => e.Type == ErrorKind.Validation))
        {
            return BuildValidationProblem(errors, controller, localizer, logger, env);
        }

        return BuildProblem(errors[0], controller, localizer, logger, env);
    }

    private static IActionResult BuildProblem(
        Error error,
        ControllerBase controller,
        IStringLocalizer localizer,
        ILogger logger,
        IHostEnvironment env)
    {
        var statusCode = MapStatus(error.Type);
        var title = Translate(error.Code, error.Args, error.Description, localizer, logger, env);
        return controller.Problem(statusCode: statusCode, title: title);
    }

    private static IActionResult BuildValidationProblem(
        List<Error> errors,
        ControllerBase controller,
        IStringLocalizer localizer,
        ILogger logger,
        IHostEnvironment env)
    {
        var modelState = new ModelStateDictionary();
        foreach (var e in errors)
        {
            var message = Translate(e.Code, e.Args, e.Description, localizer, logger, env);
            modelState.AddModelError(e.PropertyName ?? string.Empty, message);
        }

        return controller.ValidationProblem(modelState);
    }

    private static string Translate(
        string key,
        object[]? args,
        string fallback,
        IStringLocalizer localizer,
        ILogger logger,
        IHostEnvironment env)
    {
        var localized = args is { Length: > 0 } ? localizer[key, args] : localizer[key];
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

        return string.IsNullOrEmpty(fallback) ? key : fallback;
    }

    private static int MapStatus(ErrorKind kind) => kind switch
    {
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        ErrorKind.Validation => StatusCodes.Status400BadRequest,
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status500InternalServerError,
    };
}