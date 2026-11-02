namespace Taxi.Application.Features.Account.Commands.ResetPassword;

using MediatR;
using Taxi.Application.Common.Interfaces;
using Taxi.Domain.Common.Results;

/// <summary>Sets a new password from an emailed token and signs every session out.</summary>
public record ResetPasswordCommand(string Email, string Token, string NewPassword) : IRequest<Result<Success>>, ISensitiveRequest;
