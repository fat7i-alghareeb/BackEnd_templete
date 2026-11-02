namespace Taxi.Api.Infrastructure;

/// <summary>
/// Adds defensive response headers to every response. The API only ever returns JSON, so the
/// Content-Security-Policy forbids everything; the interactive docs (<c>/swagger</c>, <c>/scalar</c>,
/// Development only) need scripts and styles and get a relaxed policy instead.
/// HSTS is not set here — <c>UseHsts()</c> in <c>Program.cs</c> owns it outside Development.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string ApiContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

    private const string DocsContentSecurityPolicy =
        "default-src 'self'; script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://fonts.googleapis.com; " +
        "font-src 'self' data: https://fonts.gstatic.com https://cdn.jsdelivr.net; " +
        "img-src 'self' data: https:; connect-src 'self'; frame-ancestors 'none'";

    private readonly RequestDelegate next = next;

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers.ContentSecurityPolicy = IsDocsPath(context.Request.Path)
            ? DocsContentSecurityPolicy
            : ApiContentSecurityPolicy;

        return this.next(context);
    }

    private static bool IsDocsPath(PathString path) =>
        path.StartsWithSegments("/swagger") || path.StartsWithSegments("/scalar");
}
