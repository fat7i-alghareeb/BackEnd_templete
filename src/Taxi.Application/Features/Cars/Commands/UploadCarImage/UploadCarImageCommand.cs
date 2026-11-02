namespace Taxi.Application.Features.Cars.Commands.UploadCarImage;

using MediatR;
using Taxi.Application.Common.Interfaces;
using Taxi.Domain.Common.Results;

/// <summary>
/// Sets or replaces a car's image. <paramref name="Content"/> is owned by the caller (the controller
/// disposes it); its format is checked from its bytes, not from any client-supplied type.
/// </summary>
public record UploadCarImageCommand(Guid CarId, Stream Content, long Length) : IRequest<Result<Updated>>, ISensitiveRequest;
