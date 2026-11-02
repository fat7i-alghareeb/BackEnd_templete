namespace Taxi.Application.Features.Users.Commands.SetUserRoles;

using MediatR;
using Taxi.Domain.Common.Results;

/// <summary>
/// Replaces a user's role set. Takes effect on the user's next token (sign-in or refresh); an access
/// token already issued keeps its old role claims until it expires.
/// </summary>
public record SetUserRolesCommand(string UserId, IReadOnlyList<string> Roles) : IRequest<Result<Updated>>;
