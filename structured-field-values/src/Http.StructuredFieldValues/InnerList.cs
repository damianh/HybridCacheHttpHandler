// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues;

/// <summary>
/// Represents an inner list in a structured field value.
/// Inner lists are arrays of items with optional parameters.
/// RFC 8941 § 3.1.1
/// Mutable nodes use reference equality. Explicitly shared items remain shared.
/// </summary>
public sealed class InnerList
{
    private readonly List<StructuredFieldItem> _items = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="InnerList"/> class.
    /// </summary>
    public InnerList() => Parameters = new Parameters();

    /// <summary>
    /// Initializes a new instance of the <see cref="InnerList"/> class with items.
    /// </summary>
    /// <param name="items">The items to include in the list.</param>
    /// <param name="parameters">Parameters to copy into the owned collection.</param>
    public InnerList(IEnumerable<StructuredFieldItem> items, Parameters? parameters = null)
    {
        AddRange(items);
        Parameters = parameters is null ? new Parameters() : new Parameters(parameters);
    }

    /// <summary>
    /// Gets the parameters associated with this inner list.
    /// </summary>
    public Parameters Parameters { get; }

    /// <summary>
    /// Gets the items in this inner list.
    /// </summary>
    public IReadOnlyList<StructuredFieldItem> Items => _items.AsReadOnly();

    /// <summary>
    /// Gets the number of items in the list.
    /// </summary>
    public int Count => _items.Count;

    /// <summary>
    /// Gets the item at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index.</param>
    /// <returns>The item at the specified index.</returns>
    public StructuredFieldItem this[int index]
    {
        get => _items[index];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _items[index] = value;
        }
    }

    /// <summary>
    /// Adds an item to the inner list.
    /// </summary>
    /// <param name="item">The item to add.</param>
    public void Add(StructuredFieldItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _items.Add(item);
    }

    /// <summary>Adds a bare value wrapped in a new item.</summary>
    public void Add(BareItem value) => Add(new StructuredFieldItem(value));

    /// <summary>
    /// Adds multiple items to the inner list.
    /// </summary>
    /// <param name="items">The items to add.</param>
    public void AddRange(IEnumerable<StructuredFieldItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var snapshot = items.ToArray();
        foreach (var item in snapshot)
        {
            ArgumentNullException.ThrowIfNull(item);
        }
        _items.AddRange(snapshot);
    }

    /// <summary>
    /// Removes all items from the inner list.
    /// </summary>
    public void Clear() => _items.Clear();

    /// <summary>Returns diagnostic text, not wire output; use <see cref="StructuredFieldSerializer"/>.</summary>
    public override string ToString() => $"InnerList({Count} items; {Parameters.Count} parameters)";
}
