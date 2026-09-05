// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Microsoft.AspNetCore.Http;

namespace DamianH.Http.ForwardedHeaders;

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
