namespace Taxi.Application.Features.Identity.Queries.GetUserInfo;

using MediatR;
using Taxi.Application.Features.Identity.Dtos;
using Taxi.Domain.Common.Results;

/// <summary>
/// Deliberately NOT an <c>ICachedQuery</c>. Roles and claims drive authorization, and no command
/// in the solution evicts <c>CacheTags.UserInfo</c> — caching this would serve a stale role set
/// for the full expiration window after any permission change. Re-introduce caching only
/// together with a command that calls <c>RemoveByTagAsync(CacheTags.UserInfo, ct)</c>.
/// </summary>
public record GetUserByIdQuery(string UserId) : IRequest<Result<AppUserDto>>;
