namespace Taxi.Application.Features.Account.Commands.Logout;

using MediatR;
using Taxi.Domain.Common.Results;

/// <summary>
/// Revokes the current user's refresh token, so the session cannot be extended. The access token
/// itself stays valid until it expires (<c>JwtSettings:TokenExpirationInMinutes</c>); clients must
/// also discard it.
/// </summary>
public record LogoutCommand : IRequest<Result<Success>>;
