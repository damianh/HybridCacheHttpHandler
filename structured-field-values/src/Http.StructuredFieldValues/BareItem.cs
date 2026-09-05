// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues;

/// <summary>
/// An immutable RFC 9651 bare value. Bare values use value equality and have no parameters.
/// </summary>
public abstract class BareItem
{
    private protected BareItem() { }

    /// <summary>Gets the scalar value. Byte sequences return a defensive copy.</summary>
    public abstract object Value { get; }

    /// <summary>Gets the kind of bare value.</summary>
    public abstract ItemType Type { get; }

    /// <summary>
    /// Returns diagnostic text, not a wire representation. Use
    /// <see cref="StructuredFieldSerializer.SerializeBareItem"/> for serialization.
    /// </summary>
    public abstract override string ToString();
}
