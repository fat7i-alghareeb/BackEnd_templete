using MailKit.Net.Smtp;
using MailKit.Security;

using Microsoft.Extensions.Options;

using MimeKit;

using Taxi.Application.Common.Interfaces;
using Taxi.Application.Common.Models;

namespace Taxi.Infrastructure.Email;

/// <summary>
/// Sends through an SMTP relay with MailKit (System.Net.Mail.SmtpClient is not recommended for new
/// code). One connection per message — adequate for transactional mail; put a queue in front of it
/// (see the outbox) if volume grows.
/// </summary>
public sealed class SmtpEmailSender(IOptions<EmailSettings> settings) : IEmailSender
{
    private readonly EmailSettings settings = settings.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(this.settings.FromName, this.settings.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody }.ToMessageBody();

        var security = Enum.TryParse<SecureSocketOptions>(this.settings.Smtp.Security, ignoreCase: true, out var parsed)
            ? parsed
            : SecureSocketOptions.StartTls;

        using var client = new SmtpClient();

        // Host is non-empty: EmailSettings.Validate requires it when the provider is Smtp.
        await client.ConnectAsync(this.settings.Smtp.Host!, this.settings.Smtp.Port, security, cancellationToken);

        if (!string.IsNullOrEmpty(this.settings.Smtp.Username))
        {
            await client.AuthenticateAsync(this.settings.Smtp.Username, this.settings.Smtp.Password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
