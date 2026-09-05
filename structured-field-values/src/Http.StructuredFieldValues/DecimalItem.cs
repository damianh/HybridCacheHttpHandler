// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues;

/// <summary>
/// Represents a decimal item in a structured field value.
/// Decimals have up to 12 integer digits and up to 3 fractional digits.
/// </summary>
public sealed class DecimalItem : BareItem
{
    /// <summary>
    /// The maximum number of integer digits allowed.
    /// </summary>
    public const int MaxIntegerDigits = 12;

    /// <summary>The minimum representable decimal.</summary>
    public const decimal MinValue = -999_999_999_999.999m;

    /// <summary>The maximum representable decimal.</summary>
    public const decimal MaxValue = 999_999_999_999.999m;

    /// <summary>
    /// The maximum number of decimal places allowed.
    /// </summary>
    public const int MaxDecimalPlaces = 3;

    private readonly decimal _value;

    /// <summary>
    /// Initializes a new instance of the <see cref="DecimalItem"/> class.
    /// </summary>
    /// <param name="value">The decimal value.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the value is outside the allowed range or has unrepresentable precision.
    /// </exception>
    public DecimalItem(decimal value)
    {
        ValidateDecimal(value);
        _value = value;
    }

    /// <summary>
    /// Gets the decimal value.
    /// </summary>
    public decimal DecimalValue => _value;

    /// <inheritdoc/>
    public override object Value => _value;

    /// <inheritdoc/>
    public override ItemType Type => ItemType.Decimal;

    /// <inheritdoc/>
    public override string ToString() => _value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is DecimalItem other && _value == other._value;

    /// <inheritdoc/>
    public override int GetHashCode() => _value.GetHashCode();

    /// <summary>
    /// Implicit conversion from decimal to DecimalItem.
    /// </summary>
    public static implicit operator DecimalItem(decimal value) => new(value);

    /// <summary>
    /// Implicit conversion from DecimalItem to decimal.
    /// </summary>
    public static implicit operator decimal(DecimalItem item) => item._value;

    /// <summary>
    /// Implicit conversion from double to DecimalItem.
    /// </summary>
    public static implicit operator DecimalItem(double value) => new((decimal)value);

    private static void ValidateDecimal(decimal value)
    {
        if (value is < MinValue or > MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Decimal is outside the RFC 9651 range.");
        }

        if (decimal.Round(value, MaxDecimalPlaces) != value)
        {
            throw new ArgumentException(
                "Decimal values must be exactly representable with at most three fractional digits.",
                nameof(value));
        }
    }
}
