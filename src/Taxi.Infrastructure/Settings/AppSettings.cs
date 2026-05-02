using Taxi.Contracts.Common;

namespace Taxi.Infrastructure.Settings;

public class AppSettings
{
    public string CorsPolicyName { get; set; } = default!;

    public string[] AllowedOrigins { get; set; } = default!;

    /// <summary>
    /// Gets or sets the default language used when the Accept-Language header is absent or unsupported.
    /// Supported values: "en" (English), "ar" (Arabic). Defaults to "en".
    /// </summary>
    public string DefaultLanguage { get; set; } = Languages.Default;
}