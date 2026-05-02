namespace Taxi.Domain.Identity;

using Taxi.Domain.Common;
using Taxi.Domain.Common.Results;

public sealed class RefreshToken : AuditableEntity
{
    private RefreshToken()
    {
    }

    private RefreshToken(Guid id, string? token, string? userId, DateTimeOffset expiresOnUtc)
        : base(id)
    {
        this.Token = token;
        this.UserId = userId;
        this.ExpiresOnUtc = expiresOnUtc;
    }

    public string? Token { get; }

    public string? UserId { get; }

    public DateTimeOffset ExpiresOnUtc { get; }

    public static Result<RefreshToken> Create(Guid id, string? token, string? userId, DateTimeOffset expiresOnUtc)
    {
        if (id == Guid.Empty)
        {
            return RefreshTokenErrors.IdRequired;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return RefreshTokenErrors.TokenRequired;
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return RefreshTokenErrors.UserIdRequired;
        }

        if (expiresOnUtc <= DateTimeOffset.UtcNow)
        {
            return RefreshTokenErrors.ExpiryInvalid;
        }

        return new RefreshToken(id, token, userId, expiresOnUtc);
    }
}