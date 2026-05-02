namespace Taxi.Application.Features.Identity.Dtos;

public class TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public DateTimeOffset ExpiresOnUtc { get; set; }
}
