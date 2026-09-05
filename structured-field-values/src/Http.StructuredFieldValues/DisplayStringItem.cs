// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues;

/// <summary>An immutable RFC 9651 Unicode display string.</summary>
public sealed class DisplayStringItem : BareItem
{
    /// <summary>Creates a display string, rejecting unpaired UTF-16 surrogates.</summary>
    public DisplayStringItem(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        for (var i = 0; i < value.Length; i++)
        {
            if (!char.IsSurrogate(value[i]))
            {
                continue;
            }
            if (!char.IsHighSurrogate(value[i]) || i + 1 == value.Length || !char.IsLowSurrogate(value[++i]))
            {
                throw new ArgumentException("Display strings must contain valid Unicode scalar values.", nameof(value));
            }
        }
        StringValue = value;
    }

    /// <summary>Gets the Unicode string.</summary>
    public string StringValue { get; }

    /// <inheritdoc/>
    public override object Value => StringValue;

    /// <inheritdoc/>
    public override ItemType Type => ItemType.DisplayString;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DisplayStringItem other && StringValue == other.StringValue;

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Type, StringValue);

    /// <inheritdoc/>
    public override string ToString() => StringValue;
}
