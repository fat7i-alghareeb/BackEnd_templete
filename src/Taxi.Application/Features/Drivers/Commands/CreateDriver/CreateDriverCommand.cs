namespace Taxi.Application.Features.Drivers.Commands.CreateDriver;

using MediatR;
using Taxi.Application.Features.Drivers.Dtos;
using Taxi.Domain.Common.Results;

public record CreateDriverCommand(string FullName, string LicenseNumber) : IRequest<Result<DriverDto>>;
