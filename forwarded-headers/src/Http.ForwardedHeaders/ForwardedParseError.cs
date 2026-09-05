// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.ForwardedHeaders;

/// <summary>
/// Describes a Forwarded field syntax error without including the supplied field content.
/// </summary>
public sealed class ForwardedParseError
{
    internal ForwardedParseError(int position, string message)
    {
        Position = position;
        Message = message;
    }

    /// <summary>
    /// Gets the zero-based error offset in the comma-joined field value.
    /// An offset equal to the value length indicates an unexpected end.
    /// </summary>
    public int Position { get; }

    /// <summary>Gets a diagnostic reason that does not contain supplied header content.</summary>
    public string Message { get; }
}
