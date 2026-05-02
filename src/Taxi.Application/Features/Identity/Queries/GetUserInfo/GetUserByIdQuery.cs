namespace Taxi.Application.Features.Identity.Queries.GetUserInfo;

using MediatR;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Features.Identity.Dtos;
using Taxi.Domain.Common.Results;

public record GetUserByIdQuery(string UserId) : ICachedQuery<Result<AppUserDto>>
{
    public string CacheKey => $"user-info-{this.UserId}";

    public string[] Tags => ["user-info"];

    public TimeSpan Expiration => TimeSpan.FromMinutes(10);
}
