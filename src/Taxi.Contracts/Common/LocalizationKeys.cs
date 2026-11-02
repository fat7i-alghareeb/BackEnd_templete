namespace Taxi.Contracts.Common;

public static class LocalizationKeys
{
    public static class Car
    {
        public const string IdRequired = "Car.Id.Required";
        public const string MakeRequired = "Car.Make.Required";
        public const string ModelRequired = "Car.Model.Required";
        public const string DescriptionRequired = "Car.Description.Required";
        public const string InvalidYear = "Car.Year.Invalid";
        public const string NotFound = "Car.NotFound";
    }

    public static class RefreshToken
    {
        public const string IdRequired = "RefreshToken.Id.Required";
        public const string TokenRequired = "RefreshToken.Token.Required";
        public const string UserIdRequired = "RefreshToken.UserId.Required";
        public const string ExpiryInvalid = "RefreshToken.Expiry.Invalid";
    }

    public static class Auth
    {
        public const string ExpiredAccessTokenInvalid = "Auth.ExpiredAccessToken.Invalid";
        public const string UserIdClaimInvalid = "Auth.UserIdClaim.Invalid";
        public const string RefreshTokenExpired = "Auth.RefreshToken.Expired";
        public const string UserNotFound = "Auth.User.NotFound";
        public const string TokenGenerationFailed = "Auth.TokenGeneration.Failed";
        public const string EmailNotConfirmed = "Auth.Email.NotConfirmed";
        public const string InvalidLoginAttempt = "Auth.Login.Invalid";
    }

    public static class RateLimit
    {
        public const string Exceeded = "RateLimit.Exceeded";
    }

    public static class Validation
    {
        public const string EmailInvalid = "Validation.Email.Invalid";
        public const string EmailRequired = "Validation.Email.Required";
        public const string PasswordRequired = "Validation.Password.Required";
        public const string MakeRequired = "Validation.Make.Required";
        public const string ModelRequired = "Validation.Model.Required";
        public const string YearInvalid = "Validation.Year.Invalid";
        public const string DescriptionRequired = "Validation.Description.Required";
        public const string PageInvalid = "Validation.Page.Invalid";
        public const string PageSizeInvalid = "Validation.PageSize.Invalid";
        public const string SearchTooLong = "Validation.Search.TooLong";
        public const string SortByInvalid = "Validation.SortBy.Invalid";
        public const string SortDirectionInvalid = "Validation.SortDirection.Invalid";
    }
}