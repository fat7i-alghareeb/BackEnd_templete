using Taxi.Application.Common.Models;

namespace Taxi.Application.Common.Interfaces;

/// <summary>
/// Sends one email. Implemented in Infrastructure: <c>LoggingEmailSender</c> (default — writes the
/// message to the log, for development and tests) or <c>SmtpEmailSender</c>, chosen by
/// <c>Email:Provider</c>. Handlers depend on this, never on an SMTP library.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
