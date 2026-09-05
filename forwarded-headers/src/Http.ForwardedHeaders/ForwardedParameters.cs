// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.ForwardedHeaders;

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
