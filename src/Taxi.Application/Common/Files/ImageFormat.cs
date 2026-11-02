namespace Taxi.Application.Common.Files;

/// <summary>
/// Identifies an image by its leading bytes ("magic numbers"), never by the client-supplied
/// Content-Type or file name, which are trivially forged.
/// </summary>
public static class ImageFormat
{
    /// <summary>The largest image accepted anywhere: 5 MB.</summary>
    public const long MaxBytes = 5 * 1024 * 1024;

    /// <summary>Bytes needed by <see cref="Detect"/>.</summary>
    public const int HeaderLength = 12;

    /// <returns>(content type, extension) for JPEG, PNG or WebP; <see langword="null"/> for anything else.</returns>
    public static (string ContentType, string Extension)? Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return ("image/jpeg", ".jpg");
        }

        if (header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return ("image/png", ".png");
        }

        // "RIFF" <size> "WEBP"
        if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
        {
            return ("image/webp", ".webp");
        }

        return null;
    }

    /// <returns>The content type for a stored key's extension; <c>application/octet-stream</c> if unknown.</returns>
    public static string ContentTypeFor(string key) => Path.GetExtension(key).ToLowerInvariant() switch
    {
        ".jpg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "application/octet-stream",
    };
}
