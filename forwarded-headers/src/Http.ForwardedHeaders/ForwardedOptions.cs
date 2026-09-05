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

/// <summary>Parameters that the middleware applies from accepted hops.</summary>
[Flags]
public enum ForwardedParameters
{
    /// <summary>Do not process the header.</summary>
    None = 0,
    /// <summary>Apply the client IP address and port.</summary>
    For = 1,
    /// <summary>Validate the proxy's by identifier and expose it as metadata; never grant trust from it.</summary>
    By = 2,
    /// <summary>Apply the original host.</summary>
    Host = 4,
    /// <summary>Apply the original URI scheme.</summary>
    Proto = 8,
    /// <summary>Apply all standard parameters. Does not enable PathBase.</summary>
    All = For | By | Host | Proto,
    /// <summary>Apply the nonstandard pathbase extension, replacing Request.PathBase.</summary>
    PathBase = 16
}

/// <summary>Determines the response to malformed forwarding input.</summary>
public enum MalformedHeaderBehavior
{
    /// <summary>Continue the request, retaining only fully validated nearer hops.</summary>
    Ignore,
    /// <summary>Return HTTP 400 without applying any forwarding changes or invoking the next delegate.</summary>
    Reject
}
