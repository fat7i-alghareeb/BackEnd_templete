namespace Taxi.Application.Common.Behaviours;

using MediatR;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

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

        var result = await this.cache.GetOrCreateAsync<TResponse>(
            cacheKey,
            _ => new ValueTask<TResponse>((TResponse)(object)null!),
            new HybridCacheEntryOptions
            {
                Flags = HybridCacheEntryFlags.DisableUnderlyingData
            },
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
                        Expiration = cachedRequest.Expiration
                    },
                    cachedRequest.Tags,
                    ct);
            }
        }

        return result;
    }
}