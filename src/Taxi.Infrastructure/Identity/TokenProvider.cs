namespace Taxi.Infrastructure.Identity;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Features.Identity.Dtos;
using Taxi.Domain.Common.Results;
using Taxi.Domain.Identity;
using Taxi.Infrastructure.Settings;

public class TokenProvider(IOptions<JwtSettings> jwtSettings, IAppDbContext context, TimeProvider timeProvider) : ITokenProvider
{
    private readonly JwtSettings jwtSettings = jwtSettings.Value;
    private readonly TimeProvider timeProvider = timeProvider;
    private readonly IAppDbContext context = context;

    public async Task<Result<TokenResponse>> GenerateJwtTokenAsync(AppUserDto user, CancellationToken ct = default)
    {
        var tokenResult = await this.CreateAsync(user, ct);

        if (tokenResult.IsError)
        {
            return tokenResult.Errors;
        }

        return tokenResult.Value;
    }

    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(this.jwtSettings.Secret)),
            ValidateIssuer = true,
            ValidIssuer = this.jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = this.jwtSettings.Audience,
            ValidateLifetime = false, // Ignore token expiration
            ClockSkew = TimeSpan.Zero
        };

        var tokenHandler = new JwtSecurityTokenHandler();

        ClaimsPrincipal principal;
        SecurityToken securityToken;

        // The token comes straight from the request body: a malformed, tampered or wrongly signed
        // value is a client error, so it maps to null (→ ExpiredAccessTokenInvalid) rather than an
        // exception that would surface as a 500.
        try
        {
            principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out securityToken);
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            return null;
        }

        if (securityToken is not JwtSecurityToken jwtSecurityToken ||
            !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
        {
            return null;
        }

        return principal;
    }

    private static string GenerateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    }

    private async Task<Result<TokenResponse>> CreateAsync(AppUserDto user, CancellationToken ct = default)
    {
        var now = this.timeProvider.GetUtcNow();
        var expires = now.AddMinutes(this.jwtSettings.TokenExpirationInMinutes);

        var claims = new List<Claim>
        {
            new (JwtRegisteredClaimNames.Sub, user.UserId!),
            new (JwtRegisteredClaimNames.Email, user.Email!),
        };

        foreach (var role in user.Roles)
        {
            claims.Add(new (ClaimTypes.Role, role));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires.UtcDateTime,
            Issuer = this.jwtSettings.Issuer,
            Audience = this.jwtSettings.Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(this.jwtSettings.Secret)),
                SecurityAlgorithms.HmacSha256Signature),
        };

        var tokenHandler = new JwtSecurityTokenHandler();

        var securityToken = tokenHandler.CreateToken(descriptor);

        await this.context.RefreshTokens
              .Where(rt => rt.UserId == user.UserId)
              .ExecuteDeleteAsync(ct);

        var refreshTokenResult = RefreshToken.Create(
            Guid.NewGuid(),
            GenerateRefreshToken(),
            user.UserId,
            now.AddDays(this.jwtSettings.RefreshTokenExpirationInDays));

        if (refreshTokenResult.IsError)
        {
            return refreshTokenResult.Errors;
        }

        var refreshToken = refreshTokenResult.Value;

        this.context.RefreshTokens.Add(refreshToken);

        await this.context.SaveChangesAsync(ct);

        return new TokenResponse
        {
            AccessToken = tokenHandler.WriteToken(securityToken),
            RefreshToken = refreshToken.Token!,
            ExpiresOnUtc = expires
        };
    }
}
