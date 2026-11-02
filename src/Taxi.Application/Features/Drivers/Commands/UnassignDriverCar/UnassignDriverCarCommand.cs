namespace Taxi.Application.Features.Drivers.Commands.UnassignDriverCar;

using MediatR;
using Taxi.Domain.Common.Results;

public record UnassignDriverCarCommand(Guid DriverId) : IRequest<Result<Updated>>;
