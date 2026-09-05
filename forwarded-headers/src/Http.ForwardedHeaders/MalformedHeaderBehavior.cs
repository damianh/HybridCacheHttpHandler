// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.ForwardedHeaders;

/// <summary>Determines the response to malformed forwarding input.</summary>
public enum MalformedHeaderBehavior
{
    /// <summary>Continue the request, retaining only fully validated nearer hops.</summary>
    Ignore,
    /// <summary>Return HTTP 400 without applying any forwarding changes or invoking the next delegate.</summary>
    Reject
}
