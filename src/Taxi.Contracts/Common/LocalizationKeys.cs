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
        public const string ImageKeyRequired = "Car.ImageKey.Required";
        public const string ImageNotFound = "Car.Image.NotFound";
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

    public static class Account
    {
        public const string TokenInvalid = "Account.Token.Invalid";
        public const string CurrentPasswordInvalid = "Account.CurrentPassword.Invalid";
        public const string CannotRemoveOwnManagerRole = "Account.Roles.CannotRemoveOwnManager";
        public const string IdentityFailed = "Account.Identity.Failed";
        public const string ConfirmEmailSubject = "Account.Email.Confirm.Subject";
        public const string ConfirmEmailBody = "Account.Email.Confirm.Body";
        public const string ResetPasswordSubject = "Account.Email.ResetPassword.Subject";
        public const string ResetPasswordBody = "Account.Email.ResetPassword.Body";
        public const string AlreadyRegisteredSubject = "Account.Email.AlreadyRegistered.Subject";
        public const string AlreadyRegisteredBody = "Account.Email.AlreadyRegistered.Body";
    }

    public static class Concurrency
    {
        public const string Conflict = "Concurrency.Conflict";
    }

    public static class Idempotency
    {
        public const string KeyInvalid = "Idempotency.Key.Invalid";
        public const string KeyReused = "Idempotency.Key.Reused";
        public const string InProgress = "Idempotency.InProgress";
    }

    public static class Request
    {
        /// <summary>Formatted with the limit in bytes.</summary>
        public const string TooLarge = "Request.TooLarge";
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
        public const string MakeTooLong = "Validation.Make.TooLong";
        public const string ModelTooLong = "Validation.Model.TooLong";
        public const string DescriptionTooLong = "Validation.Description.TooLong";
        public const string PageInvalid = "Validation.Page.Invalid";
        public const string PageSizeInvalid = "Validation.PageSize.Invalid";
        public const string SearchTooLong = "Validation.Search.TooLong";
        public const string EmailTooLong = "Validation.Email.TooLong";
        public const string NewPasswordRequired = "Validation.NewPassword.Required";
        public const string CurrentPasswordRequired = "Validation.CurrentPassword.Required";
        public const string PasswordTooShort = "Validation.Password.TooShort";
        public const string PasswordRequiresDigit = "Validation.Password.RequiresDigit";
        public const string PasswordRequiresLower = "Validation.Password.RequiresLower";
        public const string PasswordRequiresUpper = "Validation.Password.RequiresUpper";
        public const string PasswordRequiresNonAlphanumeric = "Validation.Password.RequiresNonAlphanumeric";
        public const string PasswordRequiresUniqueChars = "Validation.Password.RequiresUniqueChars";
        public const string TokenRequired = "Validation.Token.Required";
        public const string FileRequired = "Validation.File.Required";
        public const string FileTooLarge = "Validation.File.TooLarge";
        public const string FileTypeInvalid = "Validation.File.TypeInvalid";
        public const string UserIdRequired = "Validation.UserId.Required";
        public const string RolesRequired = "Validation.Roles.Required";
        public const string RoleInvalid = "Validation.Role.Invalid";
        public const string SortByInvalid = "Validation.SortBy.Invalid";
        public const string SortDirectionInvalid = "Validation.SortDirection.Invalid";
    }
}