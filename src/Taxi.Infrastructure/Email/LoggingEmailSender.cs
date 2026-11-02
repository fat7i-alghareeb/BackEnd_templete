using Microsoft.Extensions.Logging;

using Taxi.Application.Common;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Common.Models;

namespace Taxi.Infrastructure.Email;

/// <summary>
/// Default <see cref="IEmailSender"/>: writes the message to the log instead of sending it, so
/// confirmation and reset links can be copied out of the console in development. The body can
/// contain one-time tokens, so never use this provider in production.
/// </summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> logger = logger;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation(
            "Email (not sent — Email:Provider is Log) to {To}: {Subject}\n{Body}",
            UtilityService.MaskEmail(message.To),
            message.Subject,
            message.HtmlBody);

        return Task.CompletedTask;
    }
}
