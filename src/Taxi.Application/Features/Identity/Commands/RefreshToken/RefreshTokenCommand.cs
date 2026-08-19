namespace Taxi.Application.Features.Identity.Commands.RefreshToken;

using MediatR;
using Taxi.Application.Features.Identity.Dtos;
using Taxi.Domain.Common.Results;

/// <summary>
/// A command, not a query: refreshing rotates the stored refresh token.
/// </summary>
public record RefreshTokenCommand(string ExpiredAccessToken, string RefreshToken) : IRequest<Result<TokenResponse>>;
