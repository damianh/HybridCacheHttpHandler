// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.ForwardedHeaders;

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
