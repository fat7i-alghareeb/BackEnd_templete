namespace Taxi.Application.Features.Account.Commands.Register;

using MediatR;
using Taxi.Application.Common.Interfaces;
using Taxi.Domain.Common.Results;

/// <summary>
/// Self-registration into <c>Roles.User</c>. Always answers the same way whether or not the email
/// already has an account, so the endpoint cannot be used to enumerate users.
/// </summary>
public record RegisterCommand(string Email, string Password) : IRequest<Result<Success>>, ISensitiveRequest;
