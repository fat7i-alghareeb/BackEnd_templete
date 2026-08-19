using MediatR;

using Taxi.Application.Features.Identity.Dtos;
using Taxi.Domain.Common.Results;

namespace Taxi.Application.Features.Identity.Commands.GenerateToken;

/// <summary>
/// A command, not a query: issuing a token rotates the user's refresh token, which deletes
/// the previous row and inserts a new one.
/// </summary>
public record GenerateTokenCommand(
    string Email,
    string Password) : IRequest<Result<TokenResponse>>;
