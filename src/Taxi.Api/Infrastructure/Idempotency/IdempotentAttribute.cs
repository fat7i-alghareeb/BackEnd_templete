using Microsoft.AspNetCore.Mvc;

namespace Taxi.Api.Infrastructure.Idempotency;

/// <summary>
/// Makes a POST action safe to retry: a request carrying an <c>Idempotency-Key</c> header is
/// executed once, and a retry with the same key replays the stored response. Requests without the
/// header are not affected. See <see cref="IdempotencyFilter"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotentAttribute : TypeFilterAttribute
{
    public IdempotentAttribute()
        : base(typeof(IdempotencyFilter))
    {
    }
}
