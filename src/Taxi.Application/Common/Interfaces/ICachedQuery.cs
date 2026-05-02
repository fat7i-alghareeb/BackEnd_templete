using MediatR;

namespace Taxi.Application.Common.Interfaces;

public interface ICachedQuery
{
    string CacheKey { get; }

    string[] Tags { get; }

    TimeSpan Expiration { get; }

    bool IsCultureAware => true;
}

public interface ICachedQuery<TResponse> : IRequest<TResponse>, ICachedQuery;