// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.ForwardedHeaders;

/// <summary>
/// An immutable, syntactically valid RFC 7239 Forwarded header.
/// </summary>
public sealed class ForwardedHeader
{
    internal ForwardedHeader(string value, List<ForwardedElement> elements)
    {
        Value = value;
        Elements = Array.AsReadOnly(elements.ToArray());
    }

    /// <summary>
    /// Gets the original field value, with multiple field values joined by commas in their original order.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the ordered elements, excluding empty comma-separated list members.
    /// Parameter values have not undergone semantic validation.
    /// </summary>
    public IReadOnlyList<ForwardedElement> Elements { get; }
}
