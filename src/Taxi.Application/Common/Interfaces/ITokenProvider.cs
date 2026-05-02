namespace Taxi.Application.Common.Interfaces;

using System.Security.Claims;
using Taxi.Application.Features.Identity.Dtos;
using Taxi.Domain.Common.Results;

public interface ITokenProvider
{
    Task<Result<TokenResponse>> GenerateJwtTokenAsync(AppUserDto user, CancellationToken ct = default);

    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
