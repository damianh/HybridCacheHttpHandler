// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Microsoft.AspNetCore.Http;

namespace DamianH.Http.ForwardedHeaders;

/// <summary>Original request state and the hops accepted by this middleware.</summary>
public interface IForwardedFeature
{
    /// <summary>Gets the remote IP address before processing, not an inbound X-Original claim.</summary>
    IPAddress? OriginalRemoteIpAddress { get; }
    /// <summary>Gets the remote port before processing.</summary>
    int OriginalRemotePort { get; }
    /// <summary>Gets the scheme before processing.</summary>
    string OriginalScheme { get; }
    /// <summary>Gets the host before processing.</summary>
    HostString OriginalHost { get; }
    /// <summary>Gets the path base before processing.</summary>
    PathString OriginalPathBase { get; }
    /// <summary>Gets accepted hops, nearest first. Rejected requests have no accepted hops.</summary>
    /// <remarks>Unselected parameters are retained as metadata but have not been semantically validated.</remarks>
    IReadOnlyList<ForwardedElement> AcceptedHops { get; }
    /// <summary>Gets why traversal stopped.</summary>
    ForwardedStopReason StopReason { get; }
    /// <summary>Gets whether processing rejected the request with HTTP 400.</summary>
    bool Rejected { get; }
}

/// <summary>Describes a forwarding processing boundary or failure.</summary>
public enum ForwardedStopReason
{
    /// <summary>All available elements were processed.</summary>
    Completed,
    /// <summary>The configured hop limit was reached.</summary>
    ForwardLimit,
    /// <summary>The peer was not a known proxy.</summary>
    UntrustedProxy,
    /// <summary>A missing, unknown, or obfuscated for value prevented further traversal.</summary>
    UnknownIdentity,
    /// <summary>The header's field syntax was invalid.</summary>
    InvalidSyntax,
    /// <summary>A considered hop had an invalid value.</summary>
    InvalidValue,
    /// <summary>A considered host was not allowed.</summary>
    DisallowedHost
}

internal sealed class ForwardedFeature(HttpContext context) : IForwardedFeature
{
    private readonly IPAddress? _originalAddress = Copy(context.Connection.RemoteIpAddress);

    public IPAddress? OriginalRemoteIpAddress => Copy(_originalAddress);
    public int OriginalRemotePort { get; } = context.Connection.RemotePort;
    public string OriginalScheme { get; } = context.Request.Scheme;
    public HostString OriginalHost { get; } = context.Request.Host;
    public PathString OriginalPathBase { get; } = context.Request.PathBase;
    public IReadOnlyList<ForwardedElement> AcceptedHops { get; internal set; } = Array.Empty<ForwardedElement>();
    public ForwardedStopReason StopReason { get; internal set; }
    public bool Rejected { get; internal set; }

    internal static IPAddress? Copy(IPAddress? address) =>
        address is null ? null
        : address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            ? new IPAddress(address.GetAddressBytes(), address.ScopeId)
            : new IPAddress(address.GetAddressBytes());
}
