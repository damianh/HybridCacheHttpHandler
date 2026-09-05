// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Linq.Expressions;

namespace DamianH.Http.StructuredFieldValues.Mapping;

/// <summary>Configures the value and parameters of an RFC 9651 item.</summary>
/// <typeparam name="T">The model class with a public parameterless constructor.</typeparam>
public sealed class ItemBuilder<T> where T : class, new()
{
    private readonly List<ParameterMapping<T>> _parameters = [];
    private readonly HashSet<string> _parameterKeys = [];

    internal ValueMapping<T>? ValueMapping { get; private set; }
    internal ParameterMapping<T>[] SnapshotParameters() => _parameters.ToArray();

    /// <summary>
    /// Maps the always-required item value. The wire type defaults to CLR type inference.
    /// </summary>
    public ItemBuilder<T> Value<TValue>(Expression<Func<T, TValue>> property, ItemType? type = null)
    {
        ArgumentNullException.ThrowIfNull(property);
        if (ValueMapping != null)
            throw new InvalidOperationException("A value mapping has already been registered for this item.");

        var (getter, setter) = PropertyAccessor.Compile(property);
        var kind = ItemTypeResolver.Resolve(typeof(TValue), type);
        ValueMapping = new ValueMapping<T>(
            v => getter(v), (instance, value) => setter(instance, (TValue)value!),
            kind, typeof(TValue));
        return this;
    }

    /// <summary>
    /// Maps a named parameter. Auto presence requires non-nullable value types only.
    /// Missing optional parameters leave property initializers unchanged.
    /// </summary>
    public ItemBuilder<T> Parameter<TValue>(
        string key,
        Expression<Func<T, TValue>> property,
        ItemType? type = null,
        MappingPresence presence = MappingPresence.Auto)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(property);
        if (!TokenItem.IsValidKey(key))
            throw new ArgumentException($"Parameter key '{key}' is not a valid RFC 9651 key.", nameof(key));
        if (_parameterKeys.Contains(key))
            throw new ArgumentException($"A parameter mapping for key '{key}' has already been registered.", nameof(key));

        var (getter, setter) = PropertyAccessor.Compile(property);
        var kind = ItemTypeResolver.Resolve(typeof(TValue), type);
        var isRequired = ItemTypeResolver.IsRequired(typeof(TValue), presence);
        _parameters.Add(new ParameterMapping<T>(
            key, v => getter(v), (instance, value) => setter(instance, (TValue)value!),
            kind, isRequired, typeof(TValue)));
        _parameterKeys.Add(key);
        return this;
    }
}
