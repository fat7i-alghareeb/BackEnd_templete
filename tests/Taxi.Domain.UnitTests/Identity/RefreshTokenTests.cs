using Taxi.Contracts.Common;
using Taxi.Domain.Identity;

using Xunit;

namespace Taxi.Domain.UnitTests.Identity;

public class RefreshTokenTests
{
    private const string ValidToken = "MFwwDQYJKoZIhvcNAQEBBQADSwAwSAJBAK";
    private const string ValidUserId = "0f3e1b2c-4d5e-6f70-8192-a3b4c5d6e7f8";

    [Fact]
    public void Create_ReturnsToken_WhenAllInputsAreValid()
    {
        var expiry = DateTimeOffset.UtcNow.AddDays(7);

        var result = RefreshToken.Create(Guid.NewGuid(), ValidToken, ValidUserId, expiry);

        Assert.True(result.IsSuccess);
        Assert.Equal(ValidToken, result.Value.Token);
        Assert.Equal(ValidUserId, result.Value.UserId);
        Assert.Equal(expiry, result.Value.ExpiresOnUtc);
    }

    [Fact]
    public void Create_Fails_WhenIdIsEmpty()
    {
        var result = RefreshToken.Create(Guid.Empty, ValidToken, ValidUserId, DateTimeOffset.UtcNow.AddDays(7));

        Assert.True(result.IsError);
        Assert.Equal(LocalizationKeys.RefreshToken.IdRequired, result.TopError.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_Fails_WhenTokenIsMissing(string? token)
    {
        var result = RefreshToken.Create(Guid.NewGuid(), token, ValidUserId, DateTimeOffset.UtcNow.AddDays(7));

        Assert.True(result.IsError);
        Assert.Equal(LocalizationKeys.RefreshToken.TokenRequired, result.TopError.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_Fails_WhenUserIdIsMissing(string? userId)
    {
        var result = RefreshToken.Create(Guid.NewGuid(), ValidToken, userId, DateTimeOffset.UtcNow.AddDays(7));

        Assert.True(result.IsError);
        Assert.Equal(LocalizationKeys.RefreshToken.UserIdRequired, result.TopError.Code);
    }

    [Fact]
    public void Create_Fails_WhenExpiryIsInThePast()
    {
        var result = RefreshToken.Create(Guid.NewGuid(), ValidToken, ValidUserId, DateTimeOffset.UtcNow.AddSeconds(-1));

        Assert.True(result.IsError);
        Assert.Equal(LocalizationKeys.RefreshToken.ExpiryInvalid, result.TopError.Code);
    }
}
