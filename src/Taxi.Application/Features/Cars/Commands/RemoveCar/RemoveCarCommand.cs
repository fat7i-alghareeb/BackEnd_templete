namespace Taxi.Application.Features.Cars.Commands.RemoveCar;

using MediatR;
using Taxi.Domain.Common.Results;

public record RemoveCarCommand(Guid Id) : IRequest<Result<Deleted>>;
