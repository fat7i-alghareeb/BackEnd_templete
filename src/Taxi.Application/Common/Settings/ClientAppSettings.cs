using System.ComponentModel.DataAnnotations;

namespace Taxi.Application.Common.Settings;

/// <summary>
/// Bound from the <c>ClientApp</c> section and validated on start. The pages of the front end that
/// emailed links open; the API appends <c>?userId=…&amp;token=…</c> (confirm) or
/// <c>?email=…&amp;token=…</c> (reset), and the page POSTs them back to <c>/api/account/*</c>.
/// </summary>
public sealed class ClientAppSettings
{
    public const string SectionName = "ClientApp";

    [Required]
    [Url]
    public string ConfirmEmailUrl { get; set; } = string.Empty;

    [Required]
    [Url]
    public string ResetPasswordUrl { get; set; } = string.Empty;
}
