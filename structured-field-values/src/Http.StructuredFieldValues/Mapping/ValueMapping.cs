// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues.Mapping;

/// <summary>
/// Describes the mapping from the bare RFC 9651 item value to a POCO property,
/// capturing compiled property accessors and the target RFC 9651 type.
/// </summary>
internal sealed class ValueMapping<T>
{
    internal ValueMapping(
        Func<T, object?> getter,
        Action<T, object?> setter,
        ValueKind kind,
        Type clrType)
    {
        Getter = getter;
        Setter = setter;
        Kind = kind;
        ClrType = clrType;
    }

    /// <summary>Compiled property getter returning a boxed value.</summary>
    internal Func<T, object?> Getter { get; }

    /// <summary>Compiled property setter accepting a boxed value.</summary>
    internal Action<T, object?> Setter { get; }

    /// <summary>RFC 9651 bare item type for the item value.</summary>
    internal ValueKind Kind { get; }

    /// <summary>The CLR type of the property (used for int/int? narrowing).</summary>
    internal Type ClrType { get; }
}
