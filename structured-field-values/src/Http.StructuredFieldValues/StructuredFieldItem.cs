// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues;

/// <summary>
/// A mutable RFC 9651 item with an immutable bare value and owned parameters.
/// Items use reference equality and can be explicitly shared between collections.
/// </summary>
public sealed class StructuredFieldItem
{
    private BareItem _value;

    /// <summary>Creates an item, copying any supplied parameters into its owned collection.</summary>
    public StructuredFieldItem(BareItem value, Parameters? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
        Parameters = parameters is null ? new Parameters() : new Parameters(parameters);
    }

    /// <summary>
    /// Gets the parameters associated with this item.
    /// Parameters are key-value pairs that provide additional metadata.
    /// </summary>
    public Parameters Parameters { get; }

    /// <summary>
    /// Gets the underlying value of this item.
    /// </summary>
    public BareItem Value
    {
        get => _value;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _value = value;
        }
    }

    /// <summary>
    /// Gets the type of this structured field item.
    /// </summary>
    public ItemType Type => Value.Type;

    /// <summary>Wraps a bare value in a new item with no parameters.</summary>
    public static implicit operator StructuredFieldItem(BareItem value) => new(value);

    /// <summary>Returns diagnostic text, not wire output; use <see cref="StructuredFieldSerializer"/>.</summary>
    public override string ToString() => $"Item({Value}; {Parameters.Count} parameters)";
}
