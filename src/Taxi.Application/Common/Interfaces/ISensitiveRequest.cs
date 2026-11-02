namespace Taxi.Application.Common.Interfaces;

/// <summary>
/// Marks a MediatR request that carries secrets or bulk content — passwords, tokens, file streams.
/// The logging, performance and unhandled-exception behaviours write only the request's type name
/// for these, never its payload. <c>SensitiveRequestConventionTests</c> fails the build when a
/// request with a password/token/stream property is not marked.
/// </summary>
public interface ISensitiveRequest;
