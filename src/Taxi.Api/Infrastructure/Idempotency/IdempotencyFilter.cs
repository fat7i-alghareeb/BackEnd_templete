using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

using Taxi.Application.Common.Interfaces;
using Taxi.Contracts.Common;

namespace Taxi.Api.Infrastructure.Idempotency;

/// <summary>
/// Implements <see cref="IdempotentAttribute"/>.
/// <list type="number">
/// <item>No <c>Idempotency-Key</c> header → the action runs normally.</item>
/// <item>The key is scoped to the caller, method and path, so one user can never replay another's
/// response.</item>
/// <item>A stored response with the same request fingerprint → replayed (status, body,
/// <c>Location</c>) with <c>Idempotency-Replayed: true</c>; the action does not run. Same key with a
/// different body → 422.</item>
/// <item>Otherwise the request takes an <see cref="IDistributedLock"/> on the key. If another request
/// with the same key holds it (still running), this one gets 409 and should retry shortly.</item>
/// <item>Only 2xx responses are stored (24 h). A failure is not, so the client may retry with the
/// same key.</item>
/// </list>
/// Responses live in HybridCache and the lock in Redis when it is configured, so the guarantee holds
/// across instances; without Redis it holds within one instance.
/// </summary>
public sealed class IdempotencyFilter(
    HybridCache cache,
    IDistributedLock distributedLock,
    IUser user,
    IStringLocalizer<SharedResource> localizer,
    ProblemDetailsFactory problemDetailsFactory,
    IOptions<JsonOptions> jsonOptions) : IAsyncActionFilter, IAsyncResultFilter
{
    public const string HeaderName = "Idempotency-Key";
    public const string ReplayedHeaderName = "Idempotency-Replayed";

    private const int MaxKeyLength = 128;
    private static readonly TimeSpan Retention = TimeSpan.FromHours(24);
    private static readonly TimeSpan LockTimeout = TimeSpan.FromMinutes(1);
    private static readonly object StateKey = new();

    private readonly HybridCache cache = cache;
    private readonly IDistributedLock distributedLock = distributedLock;
    private readonly IUser user = user;
    private readonly IStringLocalizer<SharedResource> localizer = localizer;
    private readonly ProblemDetailsFactory problemDetailsFactory = problemDetailsFactory;
    private readonly JsonSerializerOptions jsonOptions = jsonOptions.Value.JsonSerializerOptions;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;

        if (!http.Request.Headers.TryGetValue(HeaderName, out var header))
        {
            await next();
            return;
        }

        var key = header.ToString();
        if (string.IsNullOrWhiteSpace(key) || key.Length > MaxKeyLength)
        {
            context.Result = this.Problem(http, StatusCodes.Status400BadRequest, LocalizationKeys.Idempotency.KeyInvalid);
            return;
        }

        var cacheKey = $"idempotency:{this.user.Id ?? "anonymous"}:{http.Request.Method}:{http.Request.Path}:{key}";
        var fingerprint = this.Fingerprint(context.ActionArguments);

        if (await this.FindAsync(cacheKey, http.RequestAborted) is { } stored)
        {
            context.Result = this.ReplayOrReject(http, stored, fingerprint);
            return;
        }

        var handle = await this.distributedLock.TryAcquireAsync(cacheKey, LockTimeout, http.RequestAborted);
        if (handle is null)
        {
            context.Result = this.Problem(http, StatusCodes.Status409Conflict, LocalizationKeys.Idempotency.InProgress);
            return;
        }

        // The previous holder may have finished between the lookup and the lock.
        if (await this.FindAsync(cacheKey, http.RequestAborted) is { } completed)
        {
            await handle.DisposeAsync();
            context.Result = this.ReplayOrReject(http, completed, fingerprint);
            return;
        }

        // OnResultExecutionAsync stores the response and releases the lock.
        http.Items[StateKey] = new State(cacheKey, fingerprint, handle);

        var executed = await next();

        if (executed.Exception is not null && !executed.ExceptionHandled)
        {
            // Result filters do not run when the action throws.
            http.Items.Remove(StateKey);
            await handle.DisposeAsync();
        }
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        try
        {
            await next();
        }
        finally
        {
            if (context.HttpContext.Items[StateKey] is State state)
            {
                context.HttpContext.Items.Remove(StateKey);
                await using (state.Lock)
                {
                    await this.StoreIfSuccessfulAsync(context, state);
                }
            }
        }
    }

    private async Task StoreIfSuccessfulAsync(ResultExecutingContext context, State state)
    {
        var response = context.HttpContext.Response;

        // Not stored: the client may fix the request, or retry the same key after a failure.
        if (response.StatusCode is < 200 or >= 300)
        {
            return;
        }

        var body = context.Result is ObjectResult { Value: { } value }
            ? JsonSerializer.Serialize(value, value.GetType(), this.jsonOptions)
            : null;

        var location = response.Headers.Location.ToString();

        await this.cache.SetAsync(
            state.CacheKey,
            new IdempotencyRecord(state.Fingerprint, response.StatusCode, location.Length > 0 ? location : null, body),
            new HybridCacheEntryOptions { Expiration = Retention, LocalCacheExpiration = Retention },
            cancellationToken: CancellationToken.None);
    }

    private ValueTask<IdempotencyRecord?> FindAsync(string cacheKey, CancellationToken ct) =>
        this.cache.GetOrCreateAsync<IdempotencyRecord?>(
            cacheKey,
            _ => ValueTask.FromResult<IdempotencyRecord?>(null),
            new HybridCacheEntryOptions { Flags = HybridCacheEntryFlags.DisableUnderlyingData },
            cancellationToken: ct);

    private IActionResult ReplayOrReject(HttpContext http, IdempotencyRecord record, string fingerprint)
    {
        if (record.Fingerprint != fingerprint)
        {
            return this.Problem(http, StatusCodes.Status422UnprocessableEntity, LocalizationKeys.Idempotency.KeyReused);
        }

        http.Response.Headers[ReplayedHeaderName] = "true";

        if (record.Location is not null)
        {
            http.Response.Headers.Location = record.Location;
        }

        return record.Body is null
            ? new StatusCodeResult(record.StatusCode)
            : new ContentResult { StatusCode = record.StatusCode, Content = record.Body, ContentType = "application/json; charset=utf-8" };
    }

    /// <summary>SHA-256 of the bound action arguments, so a reused key with a different body is detected.</summary>
    private string Fingerprint(IDictionary<string, object?> arguments)
    {
        var relevant = arguments
            .Where(a => a.Value is not CancellationToken)
            .OrderBy(a => a.Key, StringComparer.Ordinal)
            .ToDictionary(a => a.Key, a => a.Value);

        var json = JsonSerializer.Serialize(relevant, this.jsonOptions);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    private ObjectResult Problem(HttpContext http, int status, string key)
    {
        var problem = this.problemDetailsFactory.CreateProblemDetails(http, status, this.localizer[key].Value);

        return new ObjectResult(problem) { StatusCode = status };
    }

    private sealed record State(string CacheKey, string Fingerprint, IAsyncDisposable Lock);
}
