namespace Taxi.Application.Common.Models;

/// <summary>A stored file being returned to a caller. The receiver disposes <paramref name="Content"/>.</summary>
public sealed record FileContent(Stream Content, string ContentType);
