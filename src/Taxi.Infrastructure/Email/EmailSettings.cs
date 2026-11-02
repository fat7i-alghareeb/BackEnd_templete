using System.ComponentModel.DataAnnotations;

namespace Taxi.Infrastructure.Email;

/// <summary>
/// Bound from the <c>Email</c> section and validated on start. <see cref="Provider"/> picks the
/// <c>IEmailSender</c>: <see cref="EmailProviders.Log"/> (default) or <see cref="EmailProviders.Smtp"/>.
/// </summary>
public sealed class EmailSettings : IValidatableObject
{
    public const string SectionName = "Email";

    public string Provider { get; set; } = EmailProviders.Log;

    [Required(AllowEmptyStrings = false)]
    [EmailAddress]
    public string FromAddress { get; set; } = "no-reply@taxi.local";

    public string FromName { get; set; } = "Taxi";

    public SmtpSettings Smtp { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!EmailProviders.All.Contains(this.Provider, StringComparer.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                $"Email:Provider must be one of: {string.Join(", ", EmailProviders.All)}.",
                [nameof(this.Provider)]);
        }

        if (string.Equals(this.Provider, EmailProviders.Smtp, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(this.Smtp.Host))
        {
            yield return new ValidationResult("Email:Smtp:Host is required when Email:Provider is Smtp.", ["Smtp.Host"]);
        }
    }

    public sealed class SmtpSettings
    {
        public string? Host { get; set; }

        [Range(1, 65535)]
        public int Port { get; set; } = 587;

        /// <summary>Gets or sets the security mode: <c>None</c>, <c>StartTls</c> or <c>SslOnConnect</c>.</summary>
        public string Security { get; set; } = "StartTls";

        public string? Username { get; set; }

        public string? Password { get; set; }
    }
}

public static class EmailProviders
{
    public const string Log = "Log";
    public const string Smtp = "Smtp";

    public static readonly string[] All = [Log, Smtp];
}
