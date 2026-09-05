// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Text;
using System.Text.Json;
using Shouldly;

namespace DamianH.Http.StructuredFieldValues.RfcCompliance;

internal static class RfcExpectedValue
{
    public static void AssertMatches(object actual, string? headerType, JsonElement expected)
    {
        switch (headerType)
        {
            case "item":
                AssertItem(actual.ShouldBeOfType<StructuredFieldItem>(), expected);
                break;
            case "list":
                var list = actual.ShouldBeOfType<StructuredFieldList>();
                list.Count.ShouldBe(expected.GetArrayLength());
                for (var index = 0; index < list.Count; index++)
                {
                    AssertMember(list[index], expected[index]);
                }
                break;
            case "dictionary":
                var dictionary = actual.ShouldBeOfType<StructuredFieldDictionary>();
                dictionary.Count.ShouldBe(expected.GetArrayLength());
                var members = dictionary.ToArray();
                for (var index = 0; index < members.Length; index++)
                {
                    members[index].Key.ShouldBe(expected[index][0].GetString(), $"Dictionary key at index {index}");
                    AssertMember(members[index].Value, expected[index][1]);
                }
                break;
            default:
                throw UnknownHeaderType(headerType);
        }
    }

    private static void AssertMember(StructuredFieldMember actual, JsonElement expected)
    {
        var isInnerList = expected[0].ValueKind == JsonValueKind.Array;
        actual.IsInnerList.ShouldBe(isInnerList);
        actual.IsItem.ShouldBe(!isInnerList);
        if (isInnerList)
        {
            var innerList = actual.InnerList!;
            innerList.ShouldNotBeNull();
            innerList.Count.ShouldBe(expected[0].GetArrayLength());
            for (var index = 0; index < innerList.Count; index++)
            {
                AssertItem(innerList[index], expected[0][index]);
            }

            AssertParameters(innerList.Parameters, expected[1]);
            StructuredFieldSerializer.SerializeInnerList(innerList).ShouldBe(MemberWire(expected));
        }
        else
        {
            AssertItem(actual.Item!, expected);
        }

        StructuredFieldSerializer.SerializeMember(actual).ShouldBe(MemberWire(expected));
    }

    private static void AssertItem(StructuredFieldItem actual, JsonElement expected)
    {
        actual.ShouldNotBeNull();
        actual.Type.ShouldBe(BareType(expected[0]));
        AssertBare(actual.Value, expected[0]);
        AssertParameters(actual.Parameters, expected[1]);
        StructuredFieldSerializer.SerializeItem(actual).ShouldBe(MemberWire(expected));
    }

    private static void AssertParameters(Parameters actual, JsonElement expected)
    {
        actual.Count.ShouldBe(expected.GetArrayLength());
        var parameters = actual.ToArray();
        for (var index = 0; index < parameters.Length; index++)
        {
            parameters[index].Key.ShouldBe(expected[index][0].GetString(), $"Parameter key at index {index}");
            AssertBare(parameters[index].Value, expected[index][1]);
        }
    }

    private static void AssertBare(BareItem actual, JsonElement expected)
    {
        actual.ShouldNotBeNull();
        var type = BareType(expected);
        actual.Type.ShouldBe(type);
        switch (type)
        {
            case ItemType.Integer:
                actual.ShouldBeOfType<IntegerItem>().LongValue.ShouldBe(expected.GetInt64());
                break;
            case ItemType.Decimal:
                actual.ShouldBeOfType<DecimalItem>().DecimalValue.ShouldBe(expected.GetDecimal());
                break;
            case ItemType.String:
                actual.ShouldBeOfType<StringItem>().StringValue.ShouldBe(expected.GetString());
                break;
            case ItemType.Boolean:
                actual.ShouldBeOfType<BooleanItem>().BooleanValue.ShouldBe(expected.GetBoolean());
                break;
            case ItemType.Token:
                actual.ShouldBeOfType<TokenItem>().TokenValue.ShouldBe(TaggedValue(expected).GetString());
                break;
            case ItemType.ByteSequence:
                actual.ShouldBeOfType<ByteSequenceItem>().Bytes.ToArray().ShouldBe(DecodeBinary(expected));
                break;
            case ItemType.Date:
                actual.ShouldBeOfType<DateItem>().UnixSeconds.ShouldBe(TaggedValue(expected).GetInt64());
                break;
            case ItemType.DisplayString:
                actual.ShouldBeOfType<DisplayStringItem>().StringValue.ShouldBe(TaggedValue(expected).GetString());
                break;
            default:
                throw new InvalidDataException($"Unsupported expected bare type '{type}'.");
        }

        StructuredFieldSerializer.SerializeBareItem(actual).ShouldBe(BareWire(expected));
    }

    public static object Construct(string? headerType, JsonElement expected)
    {
        switch (headerType)
        {
            case "item":
                return ConstructItem(expected);
            case "list":
                var list = new StructuredFieldList();
                foreach (var member in expected.EnumerateArray())
                {
                    list.Add(ConstructMember(member));
                }
                return list;
            case "dictionary":
                var dictionary = new StructuredFieldDictionary();
                foreach (var member in expected.EnumerateArray())
                {
                    dictionary.Add(member[0].GetString()!, ConstructMember(member[1]));
                }
                return dictionary;
            default:
                throw UnknownHeaderType(headerType);
        }
    }

    private static StructuredFieldMember ConstructMember(JsonElement expected)
    {
        if (expected[0].ValueKind != JsonValueKind.Array)
        {
            return StructuredFieldMember.FromItem(ConstructItem(expected));
        }

        var innerList = new InnerList();
        foreach (var item in expected[0].EnumerateArray())
        {
            innerList.Add(ConstructItem(item));
        }

        PopulateParameters(innerList.Parameters, expected[1]);
        return StructuredFieldMember.FromInnerList(innerList);
    }

    private static StructuredFieldItem ConstructItem(JsonElement expected)
    {
        var item = new StructuredFieldItem(ConstructBare(expected[0]));
        PopulateParameters(item.Parameters, expected[1]);
        return item;
    }

    private static void PopulateParameters(Parameters parameters, JsonElement expected)
    {
        foreach (var parameter in expected.EnumerateArray())
        {
            parameters.Add(parameter[0].GetString()!, ConstructBare(parameter[1]));
        }
    }

    private static BareItem ConstructBare(JsonElement expected) => BareType(expected) switch
    {
        ItemType.Integer => new IntegerItem(expected.GetInt64()),
        ItemType.Decimal => new DecimalItem(expected.GetDecimal()),
        ItemType.String => new StringItem(expected.GetString()!),
        ItemType.Boolean => expected.GetBoolean() ? BooleanItem.True : BooleanItem.False,
        ItemType.Token => new TokenItem(TaggedValue(expected).GetString()!),
        ItemType.ByteSequence => new ByteSequenceItem(DecodeBinary(expected)),
        ItemType.Date => new DateItem(TaggedValue(expected).GetInt64()),
        ItemType.DisplayString => new DisplayStringItem(TaggedValue(expected).GetString()!),
        _ => throw new InvalidDataException($"Unsupported expected bare value: {expected}")
    };

    // This oracle reads fixture JSON directly: no production parsing, AST construction,
    // scalar ToString, or serialization contributes to the expected wire representation.
    public static string Canonical(string? headerType, JsonElement expected) => headerType switch
    {
        "item" => MemberWire(expected),
        "list" => string.Join(", ", expected.EnumerateArray().Select(MemberWire)),
        "dictionary" => string.Join(", ", expected.EnumerateArray().Select(member =>
            member[0].GetString() + (member[1][0].ValueKind == JsonValueKind.True
                ? ParametersWire(member[1][1])
                : "=" + MemberWire(member[1])))),
        _ => throw UnknownHeaderType(headerType)
    };

    private static string MemberWire(JsonElement expected)
    {
        var value = expected[0].ValueKind == JsonValueKind.Array
            ? "(" + string.Join(" ", expected[0].EnumerateArray().Select(MemberWire)) + ")"
            : BareWire(expected[0]);
        return value + ParametersWire(expected[1]);
    }

    private static string ParametersWire(JsonElement expected) =>
        string.Concat(expected.EnumerateArray().Select(parameter =>
            ";" + parameter[0].GetString() + (parameter[1].ValueKind == JsonValueKind.True
                ? ""
                : "=" + BareWire(parameter[1]))));

    private static string BareWire(JsonElement expected) => BareType(expected) switch
    {
        ItemType.Integer => expected.GetInt64().ToString(CultureInfo.InvariantCulture),
        ItemType.Decimal => DecimalWire(expected.GetDecimal()),
        ItemType.String => "\"" + expected.GetString()!
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal) + "\"",
        ItemType.Boolean => expected.GetBoolean() ? "?1" : "?0",
        ItemType.Token => TaggedValue(expected).GetString()!,
        ItemType.ByteSequence => ":" + Convert.ToBase64String(DecodeBinary(expected)) + ":",
        ItemType.Date => "@" + TaggedValue(expected).GetInt64().ToString(CultureInfo.InvariantCulture),
        ItemType.DisplayString => DisplayStringWire(TaggedValue(expected).GetString()!),
        _ => throw new InvalidDataException($"Unsupported expected bare value: {expected}")
    };

    private static string DecimalWire(decimal value) =>
        value == decimal.Zero ? "0.0" : value.ToString("0.0##", CultureInfo.InvariantCulture);

    private static string DisplayStringWire(string value)
    {
        var wire = new StringBuilder("%\"");
        foreach (var octet in new UTF8Encoding(false, true).GetBytes(value))
        {
            if (octet is >= 0x20 and <= 0x7e and not 0x22 and not 0x25)
            {
                wire.Append((char)octet);
            }
            else
            {
                wire.Append('%').Append(octet.ToString("x2", CultureInfo.InvariantCulture));
            }
        }

        return wire.Append('"').ToString();
    }

    private static ItemType BareType(JsonElement expected) => expected.ValueKind switch
    {
        // GetDecimal/TryGetInt64 alone would erase the distinction between 1 and 1.0.
        JsonValueKind.Number => expected.GetRawText().IndexOfAny(['.', 'e', 'E']) >= 0
            ? ItemType.Decimal
            : ItemType.Integer,
        JsonValueKind.String => ItemType.String,
        JsonValueKind.True or JsonValueKind.False => ItemType.Boolean,
        JsonValueKind.Object => expected.GetProperty("__type").GetString() switch
        {
            "token" => ItemType.Token,
            "binary" => ItemType.ByteSequence,
            "date" => ItemType.Date,
            "displaystring" => ItemType.DisplayString,
            var type => throw new InvalidDataException($"Unknown tagged fixture type '{type}'.")
        },
        _ => throw new InvalidDataException($"Unsupported expected bare value: {expected}")
    };

    private static JsonElement TaggedValue(JsonElement expected) => expected.GetProperty("value");

    private static byte[] DecodeBinary(JsonElement expected)
    {
        // The upstream JSON format uses RFC 4648 Base32, not the wire format's Base64.
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var encoded = TaggedValue(expected).GetString()!;
        var payload = encoded.TrimEnd('=');
        if (payload.Length % 8 is 1 or 3 or 6 || encoded.Length % 8 != 0)
        {
            throw new InvalidDataException($"Invalid Base32 fixture length: '{encoded}'.");
        }

        var bytes = new List<byte>();
        var buffer = 0;
        var bits = 0;
        foreach (var character in payload)
        {
            var digit = alphabet.IndexOf(character);
            if (digit < 0)
            {
                throw new InvalidDataException($"Invalid Base32 fixture character '{character}'.");
            }

            buffer = (buffer << 5) | digit;
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                bytes.Add((byte)(buffer >> bits));
                buffer &= (1 << bits) - 1;
            }
        }

        if (buffer != 0)
        {
            throw new InvalidDataException($"Non-zero Base32 fixture padding bits: '{encoded}'.");
        }

        return bytes.ToArray();
    }

    private static InvalidDataException UnknownHeaderType(string? headerType) =>
        new($"Unknown fixture header type '{headerType}'.");
}
