namespace Taxi.Application.Features.Drivers.Commands.AssignDriverCar;

using MediatR;
using Taxi.Domain.Common.Results;

public record AssignDriverCarCommand(Guid DriverId, Guid CarId) : IRequest<Result<Updated>>;
