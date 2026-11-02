namespace Taxi.Application.Features.Cars.Commands.RemoveCarImage;

using MediatR;
using Taxi.Domain.Common.Results;

public record RemoveCarImageCommand(Guid CarId) : IRequest<Result<Deleted>>;
