using MediatR;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Taxi.Application.Common.Behaviours;
using Taxi.Application.Common.Caching;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.UnitTests.TestDoubles;
using Taxi.Contracts.Common;
using Taxi.Domain.Common.Results;

using Xunit;

namespace Taxi.Application.UnitTests.Common;

/// <summary>
/// Pins the two properties that only break once Redis is the L2 (both were real bugs): the probe must
/// carry the query's tags, and the stored entry must have a short L1 lifetime.
/// </summary>
public class CachingBehaviorTests
{
    private readonly HybridCache cache = Substitute.For<HybridCache>();

    [Fact]
    public async Task Probe_PassesTheQueryTags_SoAnL2HitCanStillBeEvictedByTag()
    {
        await this.Run(new TestQuery(), () => "fresh");

        var probe = this.cache.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(HybridCache.GetOrCreateAsync));
        var tags = probe.GetArguments().OfType<IEnumerable<string>>().Single();
        Assert.Contains(CacheTags.Cars, tags);
    }

    [Fact]
    public async Task Miss_StoresTheResult_WithTags_AndAnL1LifetimeCappedAtTheLocalExpiration()
    {
        await this.Run(new TestQuery(), () => "fresh");

        await this.cache.Received(1).SetAsync(
            "test-key_en",
            Arg.Any<Result<string>>(),
            Arg.Is<HybridCacheEntryOptions>(o =>
                o.Expiration == TimeSpan.FromMinutes(10)
                && o.LocalCacheExpiration == CacheDefaults.LocalExpiration),
            Arg.Is<IEnumerable<string>>(t => t.Contains(CacheTags.Cars)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Failure_IsNeverStored()
    {
        await this.Run(new TestQuery(), () => Error.NotFound("X", "missing"));

        await this.cache.DidNotReceiveWithAnyArgs().SetAsync<Result<string>>(default!, default!, default, default, default);
    }

    [Fact]
    public async Task CacheKey_IsSuffixedWithTheLanguage()
    {
        await this.Run(new TestQuery(), () => "fresh", Languages.Ar);

        await this.cache.Received(1).SetAsync(
            "test-key_ar",
            Arg.Any<Result<string>>(),
            Arg.Any<HybridCacheEntryOptions?>(),
            Arg.Any<IEnumerable<string>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void LocalExpiration_NeverExceedsTheEntryExpiration()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), CacheDefaults.LocalExpirationFor(TimeSpan.FromSeconds(5)));
        Assert.Equal(CacheDefaults.LocalExpiration, CacheDefaults.LocalExpirationFor(TimeSpan.FromHours(1)));
    }

    private Task<Result<string>> Run(TestQuery query, Func<Result<string>> handler, string language = Languages.En)
    {
        var behavior = new CachingBehavior<TestQuery, Result<string>>(
            this.cache,
            new FakeLanguageContext(language),
            NullLogger<CachingBehavior<TestQuery, Result<string>>>.Instance);

        return behavior.Handle(query, _ => Task.FromResult(handler()), default);
    }

    public sealed record TestQuery : ICachedQuery<Result<string>>
    {
        public string CacheKey => "test-key";

        public string[] Tags => [CacheTags.Cars];

        public TimeSpan Expiration => TimeSpan.FromMinutes(10);
    }
}
