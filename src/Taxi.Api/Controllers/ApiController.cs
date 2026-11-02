using System.Globalization;

using Asp.Versioning;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using Taxi.Api.Extensions;
using Taxi.Api.Infrastructure.RateLimiting;
using Taxi.Domain.Common.Results;

namespace Taxi.Api.Controllers;

/// <summary>
/// Base for every controller. Supplies <see cref="Problem(List{Error})"/> and applies
/// <see cref="RateLimitPolicies.Default"/>; an action opts into a stricter policy with its own
/// <c>[EnableRateLimiting]</c>, which wins because action metadata is more specific.
/// </summary>
[ApiController]
[EnableRateLimiting(RateLimitPolicies.Default)]
public class ApiController : ControllerBase
{
    /// <summary>
    /// Gets the API version of the current request, formatted for the <c>{version:apiVersion}</c>
    /// route segment ("1" for 1.0, "2.1" for 2.1). Pass it as the <c>version</c> route value in
    /// <c>CreatedAtAction</c> so the <c>Location</c> header points at the version the client called,
    /// never at a hard-coded one.
    /// </summary>
    protected string RequestedApiVersion =>
        this.HttpContext.RequestedApiVersion?.ToString("VVV", CultureInfo.InvariantCulture)
        ?? throw new InvalidOperationException("RequestedApiVersion is only available on versioned routes.");

    protected IActionResult Problem(List<Error> errors) => errors.ToProblem(this);
}