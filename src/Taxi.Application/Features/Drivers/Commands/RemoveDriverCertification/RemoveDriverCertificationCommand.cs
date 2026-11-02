namespace Taxi.Application.Features.Drivers.Commands.RemoveDriverCertification;

using MediatR;
using Taxi.Domain.Common.Results;

public record RemoveDriverCertificationCommand(Guid DriverId, Guid CertificationId) : IRequest<Result<Deleted>>;
