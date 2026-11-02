namespace Taxi.Application.Common.Models;

/// <summary>An outgoing email. <paramref name="HtmlBody"/> is sent as-is — encode any user input in it.</summary>
public sealed record EmailMessage(string To, string Subject, string HtmlBody);
