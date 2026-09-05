// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues;

/// <summary>
/// Represents a structured field list.
/// Lists are ordered sequences of items or inner lists.
/// RFC 8941 § 3.1
/// This mutable collection uses reference equality and retains explicitly shared nodes.
/// </summary>
public sealed class StructuredFieldList
{
    private readonly List<StructuredFieldMember> _members = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="StructuredFieldList"/> class.
    /// </summary>
    public StructuredFieldList()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StructuredFieldList"/> class with members.
    /// </summary>
    /// <param name="members">The members to include in the list.</param>
    public StructuredFieldList(IEnumerable<StructuredFieldMember> members)
    {
        AddRange(members);
    }

    /// <summary>
    /// Gets the members of this list.
    /// </summary>
    public IReadOnlyList<StructuredFieldMember> Members => _members.AsReadOnly();

    /// <summary>
    /// Gets the number of members in the list.
    /// </summary>
    public int Count => _members.Count;

    /// <summary>
    /// Gets the member at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index.</param>
    /// <returns>The member at the specified index.</returns>
    public StructuredFieldMember this[int index]
    {
        get => _members[index];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _members[index] = value;
        }
    }

    /// <summary>
    /// Adds a member to the list.
    /// </summary>
    /// <param name="member">The member to add.</param>
    public void Add(StructuredFieldMember member)
    {
        ArgumentNullException.ThrowIfNull(member);
        _members.Add(member);
    }

    /// <summary>
    /// Adds an item to the list.
    /// </summary>
    /// <param name="item">The item to add.</param>
    public void Add(StructuredFieldItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _members.Add(StructuredFieldMember.FromItem(item));
    }

    /// <summary>Adds a bare value wrapped in a new item and member.</summary>
    public void Add(BareItem value) => Add(StructuredFieldMember.FromItem(value));

    /// <summary>
    /// Adds an inner list to the list.
    /// </summary>
    /// <param name="innerList">The inner list to add.</param>
    public void Add(InnerList innerList)
    {
        ArgumentNullException.ThrowIfNull(innerList);
        _members.Add(StructuredFieldMember.FromInnerList(innerList));
    }

    /// <summary>
    /// Adds multiple members to the list.
    /// </summary>
    /// <param name="members">The members to add.</param>
    public void AddRange(IEnumerable<StructuredFieldMember> members)
    {
        ArgumentNullException.ThrowIfNull(members);
        var snapshot = members.ToArray();
        foreach (var member in snapshot)
        {
            ArgumentNullException.ThrowIfNull(member);
        }
        _members.AddRange(snapshot);
    }

    /// <summary>
    /// Removes all members from the list.
    /// </summary>
    public void Clear() => _members.Clear();

    /// <summary>Returns diagnostic text, not wire output; use <see cref="StructuredFieldSerializer"/>.</summary>
    public override string ToString() => $"List({Count} members)";
}
