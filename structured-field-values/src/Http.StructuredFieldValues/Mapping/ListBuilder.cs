// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Linq.Expressions;

namespace DamianH.Http.StructuredFieldValues.Mapping;

/// <summary>Configures elements of an RFC 9651 list.</summary>
/// <typeparam name="T">The model class with a public parameterless constructor.</typeparam>
public sealed class ListBuilder<T> where T : class, new()
{
    internal ListElementConfig<T>? ElementConfig { get; private set; }

    /// <summary>
    /// Maps primitive elements using CLR type inference or an explicit wire type.
    /// Null collections serialize as empty lists; null elements are invalid.
    /// </summary>
    public ListBuilder<T> Elements<TElement>(
        Expression<Func<T, IReadOnlyList<TElement>>> property,
        ItemType? type = null)
    {
        EnsureNoElementConfig();
        var (getter, setter) = PropertyAccessor.Compile(property);
        var kind = ItemTypeResolver.Resolve(typeof(TElement), type);
        ElementConfig = new ListElementConfig<T>(
            v => getter(v), (instance, value) => setter(instance, (IReadOnlyList<TElement>)value!),
            kind, typeof(TElement));
        return this;
    }

    /// <summary>Maps each list element through an item mapper.</summary>
    public ListBuilder<T> Elements<TElement>(
        Expression<Func<T, IReadOnlyList<TElement>>> property,
        StructuredFieldMapper<TElement> elementMapper)
        where TElement : class, new()
    {
        EnsureNoElementConfig();
        ArgumentNullException.ThrowIfNull(elementMapper);
        elementMapper.EnsureItemMapper();
        var (getter, setter) = PropertyAccessor.Compile(property);
        ElementConfig = new ListElementConfig<T>(
            v => getter(v), (instance, value) => setter(instance, (IReadOnlyList<TElement>)value!),
            default, typeof(TElement),
            item => elementMapper.ParseItem(item),
            value => elementMapper.SerializeItem((TElement)value));
        return this;
    }

    private void EnsureNoElementConfig()
    {
        if (ElementConfig != null)
            throw new InvalidOperationException("An element mapping has already been registered for this list.");
    }
}

internal sealed class ListElementConfig<T>(
    Func<T, object?> getter,
    Action<T, object?> setter,
    ValueKind elementKind,
    Type elementClrType,
    Func<StructuredFieldItem, object>? nestedParse = null,
    Func<object, StructuredFieldItem>? nestedSerialize = null)
{
    internal Func<T, object?> Getter { get; } = getter;
    internal Action<T, object?> Setter { get; } = setter;
    internal ValueKind ElementKind { get; } = elementKind;
    internal Type ElementClrType { get; } = elementClrType;
    internal Func<StructuredFieldItem, object>? NestedItemParseDelegate { get; } = nestedParse;
    internal Func<object, StructuredFieldItem>? NestedItemSerializeDelegate { get; } = nestedSerialize;
    internal bool IsNestedItem => NestedItemParseDelegate != null;
}
