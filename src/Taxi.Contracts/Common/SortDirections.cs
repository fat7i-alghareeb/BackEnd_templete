namespace Taxi.Contracts.Common;

/// <summary>Accepted values for every list endpoint's <c>sortDirection</c> query parameter (case-insensitive).</summary>
public static class SortDirections
{
    public const string Asc = "asc";
    public const string Desc = "desc";

    public static readonly string[] All = [Asc, Desc];
}
