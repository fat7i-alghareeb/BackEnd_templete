namespace Taxi.Application.Common.Behaviours;

using Taxi.Application.Common.Interfaces;

/// <summary>What the behaviours pass to <c>{@Request}</c>: the request, or a placeholder for an <see cref="ISensitiveRequest"/>.</summary>
public static class RequestLogPayload
{
    public const string Redacted = "[redacted]";

    public static object For<TRequest>(TRequest request)
        where TRequest : notnull =>
        request is ISensitiveRequest ? Redacted : request;
}
