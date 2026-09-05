// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues;

/// <summary>
/// A common list or dictionary member containing either an item or an inner list.
/// Parameters are owned by the contained node, which may be explicitly shared.
/// This wrapper uses reference equality.
/// </summary>
public sealed class StructuredFieldMember
{
    private readonly StructuredFieldItem? _item;
    private readonly InnerList? _innerList;

    private StructuredFieldMember(StructuredFieldItem? item, InnerList? innerList)
    {
        _item = item;
        _innerList = innerList;
    }

    /// <summary>
    /// Creates a member from an item without copying the mutable node.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>A new member.</returns>
    public static StructuredFieldMember FromItem(StructuredFieldItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new StructuredFieldMember(item, null);
    }

    /// <summary>
    /// Creates a member from an inner list without copying the mutable node.
    /// </summary>
    /// <param name="innerList">The inner list.</param>
    /// <returns>A new member.</returns>
    public static StructuredFieldMember FromInnerList(InnerList innerList)
    {
        ArgumentNullException.ThrowIfNull(innerList);
        return new StructuredFieldMember(null, innerList);
    }

    /// <summary>Creates a member by wrapping a bare value in a new item.</summary>
    public static StructuredFieldMember FromItem(BareItem value) => FromItem(new StructuredFieldItem(value));

    /// <summary>Gets the parameters of the contained item or inner list.</summary>
    public Parameters Parameters => IsItem ? Item.Parameters : InnerList.Parameters;

    /// <summary>
    /// Gets a value indicating whether this member is an item.
    /// </summary>
    public bool IsItem => _item != null;

    /// <summary>
    /// Gets a value indicating whether this member is an inner list.
    /// </summary>
    public bool IsInnerList => _innerList != null;

    /// <summary>
    /// Gets the item value if this member is an item.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when this member is not an item.</exception>
    public StructuredFieldItem Item => _item ?? throw new InvalidOperationException("This member is not an item.");

    /// <summary>
    /// Gets the inner list value if this member is an inner list.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when this member is not an inner list.</exception>
    public InnerList InnerList => _innerList ?? throw new InvalidOperationException("This member is not an inner list.");

    /// <summary>
    /// Tries to get the item value.
    /// </summary>
    /// <param name="item">The item if this member is an item.</param>
    /// <returns>True if this member is an item, false otherwise.</returns>
    public bool TryGetItem([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out StructuredFieldItem? item)
    {
        item = _item;
        return _item != null;
    }

    /// <summary>
    /// Tries to get the inner list value.
    /// </summary>
    /// <param name="innerList">The inner list if this member is an inner list.</param>
    /// <returns>True if this member is an inner list, false otherwise.</returns>
    public bool TryGetInnerList([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out InnerList? innerList)
    {
        innerList = _innerList;
        return _innerList != null;
    }

    /// <summary>Returns diagnostic text, not wire output; use <see cref="StructuredFieldSerializer"/>.</summary>
    public override string ToString() => IsItem ? Item.ToString()! : InnerList.ToString()!;

    /// <summary>
    /// Implicit conversion from an item to a member.
    /// </summary>
    public static implicit operator StructuredFieldMember(StructuredFieldItem item) => FromItem(item);

    /// <summary>Implicit conversion from a bare value to a member.</summary>
    public static implicit operator StructuredFieldMember(BareItem value) => FromItem(value);

    /// <summary>
    /// Implicit conversion from an inner list to a member.
    /// </summary>
    public static implicit operator StructuredFieldMember(InnerList innerList) => FromInnerList(innerList);
}
