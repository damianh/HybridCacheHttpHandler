// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues.Mapping;

/// <summary>
/// Describes a single dictionary member mapping: the RFC 9651 key, compiled property
/// accessors, the target RFC 9651 type, and whether the member is required or optional.
/// </summary>
internal sealed class MemberMapping<T>
{
    internal MemberMapping(
        string key,
        Func<T, object?> getter,
        Action<T, object?> setter,
        ValueKind kind,
        bool isRequired,
        Type clrType,
        InnerListConfig? innerList = null)
    {
        Key = key;
        Getter = getter;
        Setter = setter;
        Kind = kind;
        IsRequired = isRequired;
        ClrType = clrType;
        InnerList = innerList;
    }

    /// <summary>RFC 9651 dictionary key.</summary>
    internal string Key { get; }

    /// <summary>Compiled property getter returning a boxed value.</summary>
    internal Func<T, object?> Getter { get; }

    /// <summary>Compiled property setter accepting a boxed value.</summary>
    internal Action<T, object?> Setter { get; }

    /// <summary>The CLR type of the property (used for int/int? narrowing).</summary>
    internal Type ClrType { get; }

    /// <summary>RFC 9651 bare item type for this member.</summary>
    internal ValueKind Kind { get; }

    /// <summary>
    /// Whether absence on parse and null on serialization are rejected.
    /// </summary>
    internal bool IsRequired { get; }

    /// <summary>
    /// Inner-list configuration when this member maps to an inner list.
    /// <see langword="null"/> for simple item members.
    /// </summary>
    internal InnerListConfig? InnerList { get; }

    /// <summary>Whether this mapping represents an inner-list member.</summary>
    internal bool IsInnerList => InnerList != null;
}

/// <summary>
/// Configuration for inner-list members, capturing element kind and element POCO mapper (for nested items).
/// </summary>
internal sealed class InnerListConfig
{
    internal InnerListConfig(ValueKind elementKind, Type elementClrType)
    {
        ElementKind = elementKind;
        ElementClrType = elementClrType;
        NestedItemParseDelegate = null;
        NestedItemSerializeDelegate = null;
    }

    internal InnerListConfig(
        Type elementClrType,
        Func<StructuredFieldItem, object> nestedParse,
        Func<object, StructuredFieldItem> nestedSerialize)
    {
        ElementClrType = elementClrType;
        NestedItemParseDelegate = nestedParse;
        NestedItemSerializeDelegate = nestedSerialize;
    }

    internal ValueKind ElementKind { get; }
    internal Type ElementClrType { get; }

    /// <summary>
    /// When set, each element is a nested structured item handled by this delegate.
    /// </summary>
    internal Func<StructuredFieldItem, object>? NestedItemParseDelegate { get; }

    /// <summary>
    /// When set, each POCO element is serialized to a <see cref="StructuredFieldItem"/> by this delegate.
    /// </summary>
    internal Func<object, StructuredFieldItem>? NestedItemSerializeDelegate { get; }

    internal bool IsNestedItem => NestedItemParseDelegate != null;
}
