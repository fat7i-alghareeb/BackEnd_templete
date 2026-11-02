namespace Taxi.Application.Features.Cars.Commands.UploadCarImage;

using MediatR;
using Taxi.Domain.Common.Results;

/// <summary>
/// Sets or replaces a car's image. <paramref name="Content"/> is owned by the caller (the controller
/// disposes it); its format is checked from its bytes, not from any client-supplied type.
/// </summary>
public record UploadCarImageCommand(Guid CarId, Stream Content, long Length) : IRequest<Result<Updated>>
{
    // Keep the stream out of the request log line written by LoggingBehaviour.
    public override string ToString() => $"{nameof(UploadCarImageCommand)} {{ CarId = {this.CarId}, Length = {this.Length} }}";
}
