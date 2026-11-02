using System.Text.Json.Serialization;

using Microsoft.EntityFrameworkCore;

namespace Taxi.Application.Common.Models;

/// <summary>
/// The paging envelope returned by every collection endpoint. Never return a bare JSON array from a
/// list endpoint — adding metadata later would break every deserializer.
/// <para>
/// Serializes as <c>{ items, page, pageSize, totalCount, totalPages, hasPreviousPage, hasNextPage }</c>
/// and round-trips through System.Text.Json, which <c>CachingBehavior</c> relies on.
/// </para>
/// </summary>
/// <typeparam name="T">The item type, normally a DTO.</typeparam>
public sealed class PaginatedList<T>
{
    [JsonConstructor]
    public PaginatedList(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        this.Items = items;
        this.Page = page;
        this.PageSize = pageSize;
        this.TotalCount = totalCount;
    }

    public IReadOnlyList<T> Items { get; }

    public int Page { get; }

    public int PageSize { get; }

    public int TotalCount { get; }

    public int TotalPages => this.PageSize == 0 ? 0 : (int)Math.Ceiling(this.TotalCount / (double)this.PageSize);

    public bool HasPreviousPage => this.Page > 1;

    public bool HasNextPage => this.Page < this.TotalPages;

    /// <summary>
    /// Counts <paramref name="source"/>, then materialises one page of it. <paramref name="source"/>
    /// must already be ordered, or page boundaries are undefined.
    /// </summary>
    /// <returns>The requested page.</returns>
    public static async Task<PaginatedList<T>> CreateAsync(
        IQueryable<T> source,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        pageSize = Paging.ClampPageSize(pageSize);

        var totalCount = await source.CountAsync(cancellationToken);
        var items = await source
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedList<T>(items, page, pageSize, totalCount);
    }

    /// <summary>
    /// Projects the items in memory, keeping the paging metadata. Used when the mapping cannot be
    /// translated to SQL (for example resolving a <c>LocalizedText</c> to one language).
    /// </summary>
    /// <returns>A new list with the same metadata.</returns>
    public PaginatedList<TOut> Map<TOut>(Func<T, TOut> selector) =>
        new(this.Items.Select(selector).ToList(), this.Page, this.PageSize, this.TotalCount);
}
