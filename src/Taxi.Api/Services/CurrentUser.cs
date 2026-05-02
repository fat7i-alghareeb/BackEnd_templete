namespace Taxi.Api.Services;

using System.Security.Claims;

using Taxi.Application.Common.Interfaces;

public class CurrentUser(IHttpContextAccessor httpContextAccessor) : IUser
{
    private readonly IHttpContextAccessor httpContextAccessor = httpContextAccessor;

    public string? Id => this.httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
}