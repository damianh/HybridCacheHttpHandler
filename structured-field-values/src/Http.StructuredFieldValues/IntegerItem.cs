// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues;

/// <summary>
/// Represents an integer item in a structured field value.
/// RFC 8941 defines integers in the range of -999,999,999,999,999 to 999,999,999,999,999.
/// </summary>
public sealed class IntegerItem : BareItem
{
    /// <summary>
    /// The minimum allowed value for an RFC 8941 integer.
    /// </summary>
    public const long MinValue = -999_999_999_999_999L;

    /// <summary>
    /// The maximum allowed value for an RFC 8941 integer.
    /// </summary>
    public const long MaxValue = 999_999_999_999_999L;

    private readonly long _value;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntegerItem"/> class.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is outside the RFC 8941 allowed range.
    /// </exception>
    public IntegerItem(long value)
    {
        if (value is < MinValue or > MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"Integer value must be between {MinValue} and {MaxValue}.");
        }

        _value = value;
    }

    /// <summary>
    /// Gets the integer value.
    /// </summary>
    public long LongValue => _value;

    /// <inheritdoc/>
    public override object Value => _value;

    /// <inheritdoc/>
    public override ItemType Type => ItemType.Integer;

    /// <inheritdoc/>
    public override string ToString() => _value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is IntegerItem other && _value == other._value;

    /// <inheritdoc/>
    public override int GetHashCode() => _value.GetHashCode();

    /// <summary>
    /// Implicit conversion from long to IntegerItem.
    /// </summary>
    public static implicit operator IntegerItem(long value) => new(value);

    /// <summary>
    /// Implicit conversion from IntegerItem to long.
    /// </summary>
    public static implicit operator long(IntegerItem item) => item._value;
}
