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
