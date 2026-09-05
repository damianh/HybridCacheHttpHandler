// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace DamianH.Http.StructuredFieldValues;

/// <summary>
/// Represents a structured field dictionary.
/// Dictionaries are ordered maps from valid keys to non-null members.
/// RFC 8941 § 3.2
/// This mutable collection uses reference equality and retains explicitly shared nodes.
/// </summary>
public sealed class StructuredFieldDictionary : IEnumerable<KeyValuePair<string, StructuredFieldMember>>
{
    private readonly OrderedDictionary<string, StructuredFieldMember> _members = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="StructuredFieldDictionary"/> class.
    /// </summary>
    public StructuredFieldDictionary()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StructuredFieldDictionary"/> class with members.
    /// </summary>
    /// <param name="members">The members to include in the dictionary.</param>
    public StructuredFieldDictionary(IEnumerable<KeyValuePair<string, StructuredFieldMember>> members)
    {
        ArgumentNullException.ThrowIfNull(members);
        
        foreach (var kvp in members)
        {
            Add(kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    /// Gets the number of members in the dictionary.
    /// </summary>
    public int Count => _members.Count;

    /// <summary>
    /// Gets or sets the member with the specified key.
    /// </summary>
    /// <param name="key">The member key (must be a valid token).</param>
    /// <returns>The dictionary member.</returns>
    public StructuredFieldMember this[string key]
    {
        get => _members[key];
        set
        {
            ValidateKey(key);
            ArgumentNullException.ThrowIfNull(value);
            _members[key] = value;
        }
    }

    /// <summary>
    /// Adds a member with the specified key and value.
    /// </summary>
    /// <param name="key">The member key (must be a valid token).</param>
    /// <param name="member">The dictionary member.</param>
    public void Add(string key, StructuredFieldMember member)
    {
        ArgumentNullException.ThrowIfNull(member);
        ValidateKey(key);
        _members.Add(key, member);
    }

    /// <summary>
    /// Adds an item with the specified key.
    /// </summary>
    /// <param name="key">The member key (must be a valid token).</param>
    /// <param name="item">The item value.</param>
    public void Add(string key, StructuredFieldItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Add(key, StructuredFieldMember.FromItem(item));
    }

    /// <summary>Adds a bare value wrapped in a new item and member.</summary>
    public void Add(string key, BareItem value) => Add(key, StructuredFieldMember.FromItem(value));

    /// <summary>
    /// Adds an inner list with the specified key.
    /// </summary>
    /// <param name="key">The member key (must be a valid token).</param>
    /// <param name="innerList">The inner list value.</param>
    public void Add(string key, InnerList innerList)
    {
        ArgumentNullException.ThrowIfNull(innerList);
        Add(key, StructuredFieldMember.FromInnerList(innerList));
    }

    /// <summary>
    /// Tries to get the member with the specified key.
    /// </summary>
    /// <param name="key">The member key.</param>
    /// <param name="member">The dictionary member if found.</param>
    /// <returns>True if the member exists, false otherwise.</returns>
    public bool TryGetValue(string key, [NotNullWhen(true)] out StructuredFieldMember? member) => _members.TryGetValue(key, out member);

    /// <summary>
    /// Determines whether a member with the specified key exists.
    /// </summary>
    /// <param name="key">The member key.</param>
    /// <returns>True if the member exists, false otherwise.</returns>
    public bool ContainsKey(string key) => _members.ContainsKey(key);

    /// <summary>
    /// Removes the member with the specified key.
    /// </summary>
    /// <param name="key">The member key.</param>
    /// <returns>True if the member was removed, false otherwise.</returns>
    public bool Remove(string key) => _members.Remove(key);

    /// <summary>
    /// Removes all members from the dictionary.
    /// </summary>
    public void Clear() => _members.Clear();

    /// <summary>
    /// Gets an enumerator that iterates through the dictionary in insertion order.
    /// </summary>
    public IEnumerator<KeyValuePair<string, StructuredFieldMember>> GetEnumerator() => _members.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Returns diagnostic text, not wire output; use <see cref="StructuredFieldSerializer"/>.</summary>
    public override string ToString() => $"Dictionary({Count} members)";

    private static void ValidateKey(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        if (!TokenItem.IsValidKey(key))
        {
            throw new ArgumentException(
                $"Dictionary key '{key}' is not a valid RFC 8941 key. " +
                "Keys must start with a lowercase letter or '*' and contain only " +
                "lowercase letters, digits, '_', '-', '.', or '*'.",
                nameof(key));
        }
    }
}
