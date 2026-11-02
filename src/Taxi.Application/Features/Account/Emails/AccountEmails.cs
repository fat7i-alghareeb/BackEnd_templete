namespace Taxi.Application.Features.Account.Emails;

using System.Net;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Common.Models;
using Taxi.Contracts.Common;

/// <summary>
/// Builds the account emails. Subject and body come from the resource files through
/// <see cref="ITextLocalizer"/>, so they follow the request's Accept-Language. The only dynamic part
/// is the link, which is HTML-encoded before it is formatted into the body.
/// </summary>
public static class AccountEmails
{
    public static EmailMessage ConfirmEmail(ITextLocalizer text, string to, string baseUrl, string userId, string token) =>
        new(
            to,
            text[LocalizationKeys.Account.ConfirmEmailSubject],
            text[LocalizationKeys.Account.ConfirmEmailBody, Link(baseUrl, ("userId", userId), ("token", token))]);

    public static EmailMessage ResetPassword(ITextLocalizer text, string to, string baseUrl, string token) =>
        new(
            to,
            text[LocalizationKeys.Account.ResetPasswordSubject],
            text[LocalizationKeys.Account.ResetPasswordBody, Link(baseUrl, ("email", to), ("token", token))]);

    public static EmailMessage AlreadyRegistered(ITextLocalizer text, string to) =>
        new(
            to,
            text[LocalizationKeys.Account.AlreadyRegisteredSubject],
            text[LocalizationKeys.Account.AlreadyRegisteredBody]);

    private static string Link(string baseUrl, params (string Name, string Value)[] query)
    {
        var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        var queryString = string.Join('&', query.Select(q => $"{q.Name}={Uri.EscapeDataString(q.Value)}"));

        return WebUtility.HtmlEncode($"{baseUrl}{separator}{queryString}");
    }
}
