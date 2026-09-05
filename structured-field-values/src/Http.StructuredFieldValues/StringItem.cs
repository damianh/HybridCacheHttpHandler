// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues;

/// <summary>
/// Represents a string item in a structured field value.
/// RFC 8941 defines strings as sequences of printable ASCII characters.
/// </summary>
public sealed class StringItem : BareItem
{
    private readonly string _value;

    /// <summary>
    /// Initializes a new instance of the <see cref="StringItem"/> class.
    /// </summary>
    /// <param name="value">The string value.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when value is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when value contains non-printable ASCII characters.
    /// </exception>
    public StringItem(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidateString(value);
        _value = value;
    }

    /// <summary>
    /// Gets the string value.
    /// </summary>
    public string StringValue => _value;

    /// <inheritdoc/>
    public override object Value => _value;

    /// <inheritdoc/>
    public override ItemType Type => ItemType.String;

    /// <inheritdoc/>
    public override string ToString() => _value;

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is StringItem other && _value == other._value;

    /// <inheritdoc/>
    public override int GetHashCode() => _value.GetHashCode();

    /// <summary>
    /// Implicit conversion from string to StringItem.
    /// </summary>
    public static implicit operator StringItem(string value) => new(value);

    /// <summary>
    /// Implicit conversion from StringItem to string.
    /// </summary>
    public static implicit operator string(StringItem item) => item._value;

    private static void ValidateString(string value)
    {
        // RFC 8941: Strings are ASCII strings (0x20-0x7E)
        foreach (var c in value)
        {
            if (c < 0x20 || c > 0x7E)
            {
                throw new ArgumentException(
                    $"String contains non-printable ASCII character: 0x{(int)c:X2}. " +
                    "RFC 8941 strings must contain only printable ASCII characters (0x20-0x7E).",
                    nameof(value));
            }
        }
    }
}
