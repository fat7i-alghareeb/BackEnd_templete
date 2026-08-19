namespace Taxi.Application.Common;

/// <summary>
/// Cross-feature string helpers. Not an abstraction — it lives in Common, not Common/Interfaces.
/// </summary>
public static class UtilityService
{
    /// <summary>
    /// Masks an email for safe inclusion in logs and error descriptions:
    /// <c>fathi@example.com</c> becomes <c>f****i@example.com</c>.
    /// </summary>
    /// <returns>The masked email address.</returns>
    public static string MaskEmail(string email)
    {
        int atIndex = email.IndexOf('@');
        if (atIndex <= 1)
        {
            return $"****{email.AsSpan(atIndex)}";
        }

        return email[0] + "****" + email[atIndex - 1] + email[atIndex..];
    }
}
