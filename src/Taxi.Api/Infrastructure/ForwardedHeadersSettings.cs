using Microsoft.AspNetCore.HttpOverrides;

namespace Taxi.Api.Infrastructure;

public sealed class ForwardedHeadersSettings
{
    public ForwardedHeaders ForwardedHeaders { get; set; } =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;

    public int? ForwardLimit { get; set; } = 1;

    public bool AllowAllInDevelopment { get; set; }

    public string[]? KnownProxies { get; set; }

    public string[]? KnownIPNetworks { get; set; }
}
