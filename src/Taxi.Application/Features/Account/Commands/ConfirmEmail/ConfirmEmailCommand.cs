namespace Taxi.Application.Features.Account.Commands.ConfirmEmail;

using MediatR;
using Taxi.Domain.Common.Results;

public record ConfirmEmailCommand(string UserId, string Token) : IRequest<Result<Success>>;
