namespace Taxi.Application.Features.Cars.Dtos;

/// <summary>
/// The v2 car representation. The breaking change from <see cref="CarDto"/> (v1) that justified a
/// new API version: the description comes back in <b>both</b> languages instead of the one chosen by
/// Accept-Language (for back-office editors), and the audit timestamps are included.
/// </summary>
public record CarDetailsDto(
    Guid Id,
    string Make,
    string Model,
    int Year,
    LocalizedTextDto Description,
    bool HasImage,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastModifiedUtc);
