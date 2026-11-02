using System.Collections.Concurrent;
using System.Text.RegularExpressions;

using Taxi.Application.Common.Interfaces;
using Taxi.Application.Common.Models;

namespace Taxi.Api.IntegrationTests.Infrastructure;

/// <summary>Replaces the real sender; tests read the links out of what was "sent".</summary>
public sealed partial class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> sent = new();

    public IReadOnlyCollection<EmailMessage> Sent => this.sent;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        this.sent.Enqueue(message);
        return Task.CompletedTask;
    }

    /// <returns>The query parameters of the link in the last email sent to <paramref name="to"/>.</returns>
    public IReadOnlyDictionary<string, string> LastLinkQuery(string to)
    {
        var message = this.sent.Last(m => m.To == to);
        var href = System.Net.WebUtility.HtmlDecode(Href().Match(message.HtmlBody).Groups[1].Value);
        var query = new Uri(href).Query.TrimStart('?');

        return query.Split('&')
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(kv => kv[0], kv => Uri.UnescapeDataString(kv[1]));
    }

    [GeneratedRegex("href=\"([^\"]+)\"")]
    private static partial Regex Href();
}
