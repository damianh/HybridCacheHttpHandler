// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues;

/// <summary>
/// Represents a boolean item in a structured field value.
/// RFC 8941 represents booleans as ?1 (true) or ?0 (false).
/// </summary>
public sealed class BooleanItem : BareItem
{
    private readonly bool _value;

    /// <summary>
    /// Initializes a new instance of the <see cref="BooleanItem"/> class.
    /// </summary>
    /// <param name="value">The boolean value.</param>
    public BooleanItem(bool value) => _value = value;

    /// <summary>
    /// Gets the boolean value.
    /// </summary>
    public bool BooleanValue => _value;

    /// <inheritdoc/>
    public override object Value => _value;

    /// <inheritdoc/>
    public override ItemType Type => ItemType.Boolean;

    /// <inheritdoc/>
    public override string ToString() => _value ? "?1" : "?0";

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is BooleanItem other && _value == other._value;

    /// <inheritdoc/>
    public override int GetHashCode() => _value.GetHashCode();

    /// <summary>
    /// Implicit conversion from bool to BooleanItem.
    /// </summary>
    public static implicit operator BooleanItem(bool value) => new(value);

    /// <summary>
    /// Implicit conversion from BooleanItem to bool.
    /// </summary>
    public static implicit operator bool(BooleanItem item) => item._value;

    /// <summary>
    /// Gets a BooleanItem representing true.
    /// </summary>
    public static BooleanItem True { get; } = new(true);

    /// <summary>
    /// Gets a BooleanItem representing false.
    /// </summary>
    public static BooleanItem False { get; } = new(false);
}
