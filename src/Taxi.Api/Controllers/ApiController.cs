using Microsoft.AspNetCore.Mvc;

using Taxi.Api.Extensions;
using Taxi.Domain.Common.Results;

namespace Taxi.Api.Controllers;

[ApiController]
public class ApiController : ControllerBase
{
    protected IActionResult Problem(List<Error> errors) => errors.ToProblem(this);
}