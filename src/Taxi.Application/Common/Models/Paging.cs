namespace Taxi.Application.Common.Models;

/// <summary>
/// Paging limits shared by every list query. A requested page size above <see cref="MaxPageSize"/>
/// is clamped rather than rejected; a page or page size below 1 is a validation error.
/// </summary>
public static class Paging
{
    public const int DefaultPage = 1;

    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 100;

    public static int ClampPageSize(int pageSize) => Math.Clamp(pageSize, 1, MaxPageSize);
}
