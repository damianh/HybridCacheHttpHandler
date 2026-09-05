// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;

namespace DamianH.Http.ForwardedHeaders;

/// <summary>Configures RFC 7239 forwarding, independently of X-Forwarded-* middleware.</summary>
public sealed class ForwardedOptions
{
    /// <summary>Gets or sets the parameters to apply. No parameters are enabled by default.</summary>
    public ForwardedParameters Parameters { get; set; }

    /// <summary>Gets or sets the maximum number of hops. Defaults to one; null removes the limit.</summary>
    public int? ForwardLimit { get; set; } = 1;

    /// <summary>Gets trusted proxy addresses. Defaults to IPv6 loopback.</summary>
    /// <remarks>Emptying this list and KnownIPNetworks disables proxy trust checks.</remarks>
    public IList<IPAddress> KnownProxies { get; } = new List<IPAddress> { IPAddress.IPv6Loopback };

    /// <summary>Gets trusted proxy networks. Defaults to the IPv4 loopback network.</summary>
    /// <remarks>Emptying this list and KnownProxies disables proxy trust checks.</remarks>
    public IList<IPNetwork> KnownIPNetworks { get; } = new List<IPNetwork> { IPNetwork.Parse("127.0.0.0/8") };

    /// <summary>Gets allowed forwarded hosts, without ports. An empty list allows any valid host.</summary>
    /// <remarks>Supports IDNs and subdomain wildcards. A wildcard does not match the parent domain.</remarks>
    public IList<string> AllowedHosts { get; } = new List<string>();

    /// <summary>Gets or sets how malformed input is handled. Defaults to Ignore.</summary>
    public MalformedHeaderBehavior MalformedHeaderBehavior { get; set; }

    /// <summary>Gets or sets the incoming header name. Defaults to Forwarded.</summary>
    public string HeaderName { get; set; } = "Forwarded";

    /// <summary>Gets or sets the original remote endpoint header name.</summary>
    public string OriginalForHeaderName { get; set; } = "X-Original-For";

    /// <summary>Gets or sets the original host header name.</summary>
    public string OriginalHostHeaderName { get; set; } = "X-Original-Host";

    /// <summary>Gets or sets the original scheme header name.</summary>
    public string OriginalProtoHeaderName { get; set; } = "X-Original-Proto";

    /// <summary>Gets or sets the original path base header name.</summary>
    public string OriginalPrefixHeaderName { get; set; } = "X-Original-Prefix";
}
