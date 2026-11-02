using System.ComponentModel.DataAnnotations;

namespace Taxi.Infrastructure.Settings;

/// <summary>
/// Bound from the <c>JwtSettings</c> section and validated with <c>ValidateOnStart</c>: a missing or
/// short secret stops the host at boot instead of failing on the first token request.
/// </summary>
public sealed class JwtSettings
{
    public const string SectionName = "JwtSettings";

    /// <summary>
    /// HMAC-SHA256 signing key. At least 32 characters — 256 bits is the minimum the algorithm
    /// accepts, and every character is at least one UTF-8 byte.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [MinLength(32)]
    public string Secret { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Issuer { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Audience { get; set; } = string.Empty;

    [Range(1, 1440)]
    public int TokenExpirationInMinutes { get; set; } = 60;

    [Range(1, 365)]
    public int RefreshTokenExpirationInDays { get; set; } = 7;
}
