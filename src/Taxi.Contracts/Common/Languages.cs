namespace Taxi.Contracts.Common;

/// <summary>
/// Strongly-typed registry of supported language codes. Use these constants
/// instead of raw string literals ("en"/"ar") anywhere a language code is
/// required — Accept-Language resolution, JSONB projection branches,
/// RequestLocalization registration, etc.
///
/// Adding a new language: add a constant, append to <see cref="All"/>, then
/// extend <c>LocalizedText</c> and the per-handler switch expressions.
/// </summary>
public static class Languages
{
    public const string En = "en";
    public const string Ar = "ar";

    public const string Default = En;

    public static readonly string[] All = [En, Ar];
}