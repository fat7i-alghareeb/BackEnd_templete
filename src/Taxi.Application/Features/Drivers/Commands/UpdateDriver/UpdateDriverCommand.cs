namespace Taxi.Application.Features.Drivers.Commands.UpdateDriver;

using MediatR;
using Taxi.Domain.Common.Results;

public record UpdateDriverCommand(Guid Id, string FullName, string LicenseNumber) : IRequest<Result<Updated>>;
