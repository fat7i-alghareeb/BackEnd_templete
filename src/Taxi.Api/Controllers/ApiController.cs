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
    protected IActionResult Problem(List<Error> errors) => errors.ToProblem(this);
}