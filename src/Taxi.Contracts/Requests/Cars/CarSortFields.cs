namespace Taxi.Contracts.Requests.Cars;

/// <summary>
/// Accepted values for <see cref="GetCarsRequest.SortBy"/> (case-insensitive). Anything else is a
/// 400 — sorting is a whitelist, never a column name passed through to SQL.
/// </summary>
public static class CarSortFields
{
    public const string Make = "make";
    public const string Model = "model";
    public const string Year = "year";
    public const string CreatedAt = "createdAt";

    public const string Default = CreatedAt;

    public static readonly string[] All = [Make, Model, Year, CreatedAt];
}
