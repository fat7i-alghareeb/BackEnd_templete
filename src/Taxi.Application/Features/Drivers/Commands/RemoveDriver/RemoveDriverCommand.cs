namespace Taxi.Application.Features.Drivers.Commands.RemoveDriver;

using MediatR;
using Taxi.Domain.Common.Results;

public record RemoveDriverCommand(Guid Id) : IRequest<Result<Deleted>>;
