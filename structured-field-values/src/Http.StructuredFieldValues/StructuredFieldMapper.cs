// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DamianH.Http.StructuredFieldValues.Mapping;

namespace DamianH.Http.StructuredFieldValues;

/// <summary>Maps RFC 9651 structured fields to and from a model class.</summary>
/// <typeparam name="T">The model class with a public parameterless constructor.</typeparam>
/// <remarks>
/// Configuration is frozen at construction. Instances can be reused concurrently with independent
/// model instances; concurrent mutation of the same model or AST is not supported.
/// Unmapped members and parameters are ignored, not preserved for reserialization.
/// </remarks>
public sealed class StructuredFieldMapper<T> where T : class, new()
{
    private readonly Func<string, T> _parse;
    private readonly Func<T, string> _serialize;
    private readonly Func<StructuredFieldItem, T>? _itemParse;
    private readonly Func<T, StructuredFieldItem>? _itemSerialize;
    private readonly FieldKind _fieldKind;

    private enum FieldKind { Dictionary, List, Item }

    private StructuredFieldMapper(
        FieldKind fieldKind,
        Func<string, T> parse,
        Func<T, string> serialize,
        Func<StructuredFieldItem, T>? itemParse = null,
        Func<T, StructuredFieldItem>? itemSerialize = null)
    {
        _fieldKind = fieldKind;
        _parse = parse;
        _serialize = serialize;
        _itemParse = itemParse;
        _itemSerialize = itemSerialize;
    }

    /// <summary>Creates a dictionary mapper from a snapshot of the configuration.</summary>
    public static StructuredFieldMapper<T> Dictionary(Action<DictionaryBuilder<T>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new DictionaryBuilder<T>();
        configure(builder);
        var members = builder.SnapshotMembers();
        var parse = DictionaryMapperFactory.BuildParseDelegate(members);
        var serialize = DictionaryMapperFactory.BuildSerializeDelegate(members);
        return new StructuredFieldMapper<T>(
            FieldKind.Dictionary,
            input => parse(StructuredFieldParser.ParseDictionary(input)),
            value => StructuredFieldSerializer.SerializeDictionary(serialize(value)));
    }

    /// <summary>Creates a list mapper. An Elements mapping is required.</summary>
    public static StructuredFieldMapper<T> List(Action<ListBuilder<T>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new ListBuilder<T>();
        configure(builder);
        var config = builder.ElementConfig
            ?? throw new InvalidOperationException("No element mapping was registered. Call Elements() to configure the list.");
        var parse = ListMapperFactory.BuildParseDelegate(config);
        var serialize = ListMapperFactory.BuildSerializeDelegate(config);
        return new StructuredFieldMapper<T>(
            FieldKind.List,
            input => parse(StructuredFieldParser.ParseList(input)),
            value => StructuredFieldSerializer.SerializeList(serialize(value)));
    }

    /// <summary>Creates an item mapper. A Value mapping is required, including for Boolean flag items.</summary>
    public static StructuredFieldMapper<T> Item(Action<ItemBuilder<T>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new ItemBuilder<T>();
        configure(builder);
        var valueMapping = builder.ValueMapping
            ?? throw new InvalidOperationException("No value mapping was registered. Call Value() to configure the item.");
        var parameters = builder.SnapshotParameters();
        var parse = ItemMapperFactory.BuildParseDelegate(valueMapping, parameters);
        var serialize = ItemMapperFactory.BuildSerializeDelegate(valueMapping, parameters);
        return new StructuredFieldMapper<T>(
            FieldKind.Item,
            input => parse(StructuredFieldParser.ParseItem(input)),
            value => StructuredFieldSerializer.SerializeItem(serialize(value)),
            parse, serialize);
    }

    /// <summary>
    /// Parses a field. Malformed input, missing required values and mapping mismatches throw
    /// <see cref="StructuredFieldParseException"/>.
    /// </summary>
    public T Parse(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return _parse(input);
    }

    /// <summary>
    /// Returns false for null, malformed input or mapping mismatches.
    /// Empty lists and dictionaries are accepted if required mappings permit them.
    /// User constructor and property accessor exceptions are not suppressed.
    /// </summary>
    public bool TryParse(string? input, [NotNullWhen(true)] out T? result)
    {
        result = null;
        if (input is null)
            return false;
        try
        {
            result = Parse(input);
            return true;
        }
        catch (StructuredFieldParseException)
        {
            return false;
        }
    }

    /// <summary>Serializes the configured projection to canonical RFC 9651 syntax.</summary>
    public string Serialize(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return _serialize(value);
    }

    internal void EnsureItemMapper()
    {
        if (_fieldKind != FieldKind.Item)
            throw new InvalidOperationException("Nested element mappings require a mapper created via Item().");
    }

    internal T ParseItem(StructuredFieldItem item)
    {
        EnsureItemMapper();
        return _itemParse!(item);
    }

    internal StructuredFieldItem SerializeItem(T value)
    {
        EnsureItemMapper();
        ArgumentNullException.ThrowIfNull(value);
        return _itemSerialize!(value);
    }
}
