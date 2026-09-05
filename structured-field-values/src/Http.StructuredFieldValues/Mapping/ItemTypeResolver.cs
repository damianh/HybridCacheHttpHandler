// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues.Mapping;

internal static class ItemTypeResolver
{
    internal static ValueKind Resolve(Type clrType, ItemType? type = null)
    {
        var underlying = Nullable.GetUnderlyingType(clrType) ?? clrType;
        var inferred = underlying == typeof(int) || underlying == typeof(long) ? ItemType.Integer
            : underlying == typeof(decimal) ? ItemType.Decimal
            : underlying == typeof(bool) ? ItemType.Boolean
            : underlying == typeof(string) ? ItemType.String
            : underlying == typeof(byte[]) ? ItemType.ByteSequence
            : throw new NotSupportedException(
                $"CLR type '{clrType.Name}' has no RFC 9651 mapping. " +
                "Supported types: int, long, decimal, bool, string, byte[].");

        var selected = type ?? inferred;
        if (selected != inferred &&
            !(underlying == typeof(string) && selected is ItemType.Token or ItemType.DisplayString) &&
            !(inferred == ItemType.Integer && selected == ItemType.Date))
        {
            throw new ArgumentException(
                $"Wire type '{selected}' cannot be mapped to CLR type '{clrType.Name}'.", nameof(type));
        }

        return selected switch
        {
            ItemType.Integer => ValueKind.Integer,
            ItemType.Decimal => ValueKind.Decimal,
            ItemType.Boolean => ValueKind.Boolean,
            ItemType.String => ValueKind.String,
            ItemType.Token => ValueKind.Token,
            ItemType.ByteSequence => ValueKind.ByteSequence,
            ItemType.Date => ValueKind.Date,
            ItemType.DisplayString => ValueKind.DisplayString,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    internal static bool IsRequired(Type clrType, MappingPresence presence) => presence switch
    {
        MappingPresence.Auto => clrType.IsValueType && Nullable.GetUnderlyingType(clrType) is null,
        MappingPresence.Required => true,
        MappingPresence.Optional => false,
        _ => throw new ArgumentOutOfRangeException(nameof(presence))
    };

    internal static BareItem ToItem(ValueKind kind, object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return kind switch
        {
            ValueKind.Integer => new IntegerItem(value is int i ? i : (long)value),
            ValueKind.Decimal => new DecimalItem((decimal)value),
            ValueKind.Boolean => (bool)value ? BooleanItem.True : BooleanItem.False,
            ValueKind.String => new StringItem((string)value),
            ValueKind.Token => new TokenItem((string)value),
            ValueKind.ByteSequence => new ByteSequenceItem((byte[])value),
            ValueKind.Date => new DateItem(value is int seconds ? seconds : (long)value),
            ValueKind.DisplayString => new DisplayStringItem((string)value),
            _ => throw new NotSupportedException($"Unsupported ValueKind: {kind}")
        };
    }

    internal static object ExtractValue(ValueKind kind, BareItem item, Type targetType, string context)
    {
        object result = (kind, item) switch
        {
            (ValueKind.Integer, IntegerItem value) => value.LongValue,
            (ValueKind.Decimal, DecimalItem value) => value.DecimalValue,
            (ValueKind.Boolean, BooleanItem value) => value.BooleanValue,
            (ValueKind.String, StringItem value) => value.StringValue,
            (ValueKind.Token, TokenItem value) => value.TokenValue,
            (ValueKind.ByteSequence, ByteSequenceItem value) => value.ToArray(),
            (ValueKind.Date, DateItem value) => value.UnixSeconds,
            (ValueKind.DisplayString, DisplayStringItem value) => value.StringValue,
            _ => throw new StructuredFieldParseException(
                $"Expected a {kind} for {context}, but found {item.Type}.")
        };

        if (result is long number && (Nullable.GetUnderlyingType(targetType) ?? targetType) == typeof(int))
        {
            if (number < int.MinValue || number > int.MaxValue)
                throw new StructuredFieldParseException(
                    $"{kind} value {number} for {context} overflows Int32 (range {int.MinValue}..{int.MaxValue}).");
            return (int)number;
        }

        return result;
    }
}
