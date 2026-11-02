namespace Taxi.Application.Features.Cars.Queries.GetCarImage;

using MediatR;
using Taxi.Application.Common.Models;
using Taxi.Domain.Common.Results;

/// <summary>Not cached: the payload is a stream, and HTTP caching headers are the better tool for images.</summary>
public record GetCarImageQuery(Guid CarId) : IRequest<Result<FileContent>>;
