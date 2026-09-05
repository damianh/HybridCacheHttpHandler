// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Linq.Expressions;

namespace DamianH.Http.StructuredFieldValues.Mapping;

/// <summary>Configures members of an RFC 9651 dictionary.</summary>
/// <typeparam name="T">The model class with a public parameterless constructor.</typeparam>
public sealed class DictionaryBuilder<T> where T : class, new()
{
    private readonly List<MemberMapping<T>> _members = [];
    private readonly HashSet<string> _keys = [];

    internal MemberMapping<T>[] SnapshotMembers() => _members.ToArray();

    /// <summary>
    /// Maps a member using CLR type inference or an explicit wire type.
    /// Auto presence requires non-nullable value types only.
    /// Missing optional members leave property initializers unchanged.
    /// </summary>
    public DictionaryBuilder<T> Member<TValue>(
        string key,
        Expression<Func<T, TValue>> property,
        ItemType? type = null,
        MappingPresence presence = MappingPresence.Auto)
    {
        ValidateKey(key);
        var (getter, setter) = PropertyAccessor.Compile(property);
        var kind = ItemTypeResolver.Resolve(typeof(TValue), type);
        var isRequired = ItemTypeResolver.IsRequired(typeof(TValue), presence);
        _members.Add(new MemberMapping<T>(
            key, v => getter(v), (instance, value) => setter(instance, (TValue)value!),
            kind, isRequired, typeof(TValue)));
        _keys.Add(key);
        return this;
    }

    /// <summary>Maps a dictionary member to a primitive inner list, optional by default.</summary>
    public DictionaryBuilder<T> InnerList<TElement>(
        string key,
        Expression<Func<T, IReadOnlyList<TElement>?>> property,
        ItemType? type = null,
        MappingPresence presence = MappingPresence.Auto)
    {
        ValidateKey(key);
        var (getter, setter) = PropertyAccessor.Compile(property);
        var kind = ItemTypeResolver.Resolve(typeof(TElement), type);
        var isRequired = ItemTypeResolver.IsRequired(typeof(IReadOnlyList<TElement>), presence);
        _members.Add(new MemberMapping<T>(
            key, v => getter(v), (instance, value) => setter(instance, (IReadOnlyList<TElement>?)value),
            kind, isRequired, typeof(IReadOnlyList<TElement>),
            new InnerListConfig(kind, typeof(TElement))));
        _keys.Add(key);
        return this;
    }

    /// <summary>Maps an inner list through an item mapper, optional by default.</summary>
    public DictionaryBuilder<T> InnerList<TElement>(
        string key,
        Expression<Func<T, IReadOnlyList<TElement>?>> property,
        StructuredFieldMapper<TElement> elementMapper,
        MappingPresence presence = MappingPresence.Auto)
        where TElement : class, new()
    {
        ValidateKey(key);
        ArgumentNullException.ThrowIfNull(elementMapper);
        elementMapper.EnsureItemMapper();
        var (getter, setter) = PropertyAccessor.Compile(property);
        var isRequired = ItemTypeResolver.IsRequired(typeof(IReadOnlyList<TElement>), presence);
        _members.Add(new MemberMapping<T>(
            key, v => getter(v), (instance, value) => setter(instance, (IReadOnlyList<TElement>?)value),
            default, isRequired, typeof(IReadOnlyList<TElement>),
            new InnerListConfig(
                typeof(TElement),
                item => elementMapper.ParseItem(item),
                value => elementMapper.SerializeItem((TElement)value))));
        _keys.Add(key);
        return this;
    }

    private void ValidateKey(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        if (!TokenItem.IsValidKey(key))
            throw new ArgumentException($"Dictionary key '{key}' is not a valid RFC 9651 key.", nameof(key));
        if (_keys.Contains(key))
            throw new ArgumentException($"A mapping for dictionary key '{key}' has already been registered.", nameof(key));
    }
}
