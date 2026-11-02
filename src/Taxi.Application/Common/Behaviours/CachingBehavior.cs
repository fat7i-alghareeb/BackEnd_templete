namespace Taxi.Application.Common.Behaviours;

using MediatR;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

using Taxi.Application.Common.Caching;
using Taxi.Application.Common.Interfaces;
using Taxi.Domain.Common.Results.Abstractions;

public class CachingBehavior<TRequest, TResponse>(
    HybridCache cache,
    ILanguageContext languageContext,
    ILogger<CachingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly HybridCache cache = cache;
    private readonly ILanguageContext languageContext = languageContext;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (request is not ICachedQuery cachedRequest)
        {
            return await next(ct);
        }

        this.logger.LogInformation("Checking cache for {RequestName}", typeof(TRequest).Name);

        var cacheKey = cachedRequest.IsCultureAware
            ? $"{cachedRequest.CacheKey}_{this.languageContext.Language}"
            : cachedRequest.CacheKey;

        // Probe only (DisableUnderlyingData: the factory never runs), so a failed Result is never
        // stored. The tags MUST be passed here too: on an L2 (Redis) hit HybridCache copies the value
        // into L1 with the tags of *this* call, and an L1 entry without tags is never evicted by
        // RemoveByTagAsync — it would serve stale data until LocalCacheExpiration.
        var result = await this.cache.GetOrCreateAsync<TResponse>(
            cacheKey,
            _ => new ValueTask<TResponse>((TResponse)(object)null!),
            new HybridCacheEntryOptions
            {
                Flags = HybridCacheEntryFlags.DisableUnderlyingData,
            },
            cachedRequest.Tags,
            cancellationToken: ct);

        if (result is null)
        {
            result = await next(ct);

            if (result is IResult res && res.IsSuccess)
            {
                this.logger.LogInformation("Caching result for {RequestName}", typeof(TRequest).Name);

                await this.cache.SetAsync(
                    cacheKey,
                    result,
                    new HybridCacheEntryOptions
                    {
                        Expiration = cachedRequest.Expiration,

                        // Per-call options do not inherit DefaultEntryOptions: without this, L1
                        // would fall back to Expiration (10 min) and other nodes would serve a
                        // stale copy for that long after an invalidation.
                        LocalCacheExpiration = CacheDefaults.LocalExpirationFor(cachedRequest.Expiration),
                    },
                    cachedRequest.Tags,
                    ct);
            }
        }

        return result;
    }
}