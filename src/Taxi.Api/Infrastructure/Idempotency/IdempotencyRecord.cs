namespace Taxi.Api.Infrastructure.Idempotency;

/// <summary>The stored outcome of the first request made with an idempotency key.</summary>
public sealed record IdempotencyRecord(string Fingerprint, int StatusCode, string? Location, string? Body);
