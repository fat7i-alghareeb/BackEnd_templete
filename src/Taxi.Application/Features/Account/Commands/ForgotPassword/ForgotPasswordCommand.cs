namespace Taxi.Application.Features.Account.Commands.ForgotPassword;

using MediatR;
using Taxi.Domain.Common.Results;

/// <summary>Always succeeds; a reset email goes out only for an existing, confirmed account.</summary>
public record ForgotPasswordCommand(string Email) : IRequest<Result<Success>>;
