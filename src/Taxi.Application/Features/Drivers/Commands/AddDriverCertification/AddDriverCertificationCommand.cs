namespace Taxi.Application.Features.Drivers.Commands.AddDriverCertification;

using MediatR;
using Taxi.Application.Features.Drivers.Dtos;
using Taxi.Domain.Common.Results;

public record AddDriverCertificationCommand(Guid DriverId, string NameEn, string NameAr, DateOnly ExpiresOn)
    : IRequest<Result<DriverCertificationDto>>;
