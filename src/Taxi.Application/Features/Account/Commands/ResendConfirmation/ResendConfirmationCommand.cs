namespace Taxi.Application.Features.Account.Commands.ResendConfirmation;

using MediatR;
using Taxi.Domain.Common.Results;

/// <summary>Always succeeds; an email goes out only for an existing, unconfirmed account.</summary>
public record ResendConfirmationCommand(string Email) : IRequest<Result<Success>>;
