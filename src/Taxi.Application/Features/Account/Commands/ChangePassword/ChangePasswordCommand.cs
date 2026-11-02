namespace Taxi.Application.Features.Account.Commands.ChangePassword;

using MediatR;
using Taxi.Application.Common.Interfaces;
using Taxi.Domain.Common.Results;

/// <summary>Changes the current user's password (from <c>IUser</c>) and revokes their refresh tokens.</summary>
public record ChangePasswordCommand(string CurrentPassword, string NewPassword) : IRequest<Result<Success>>, ISensitiveRequest;
