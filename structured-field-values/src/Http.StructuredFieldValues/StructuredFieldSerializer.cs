// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Text;

namespace DamianH.Http.StructuredFieldValues;

/// <summary>Serializes structured fields to RFC 9651 canonical wire format, without using diagnostic ToString methods.</summary>
public static class StructuredFieldSerializer
{
    /// <summary>Serializes a bare value without parameters.</summary>
    public static string SerializeBareItem(BareItem value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var output = new StringBuilder();
        WriteBareItem(value, output);
        return output.ToString();
    }

    /// <summary>Serializes an item, including its parameters.</summary>
    public static string SerializeItem(StructuredFieldItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var output = new StringBuilder();
        WriteItem(item, output);
        return output.ToString();
    }

    /// <summary>Serializes a member's item or inner list, without any dictionary key.</summary>
    public static string SerializeMember(StructuredFieldMember member)
    {
        ArgumentNullException.ThrowIfNull(member);
        var output = new StringBuilder();
        WriteMember(member, output);
        return output.ToString();
    }

    /// <summary>Serializes an inner list, including its items' and its own parameters.</summary>
    public static string SerializeInnerList(InnerList innerList)
    {
        ArgumentNullException.ThrowIfNull(innerList);
        var output = new StringBuilder();
        WriteInnerList(innerList, output);
        return output.ToString();
    }

    /// <summary>Serializes a list, preserving member and parameter order.</summary>
    public static string SerializeList(StructuredFieldList list)
    {
        ArgumentNullException.ThrowIfNull(list);
        var output = new StringBuilder();
        for (var i = 0; i < list.Count; i++)
        {
            if (i != 0)
            {
                output.Append(", ");
            }
            WriteMember(list[i], output);
        }
        return output.ToString();
    }

    /// <summary>Serializes an ordered dictionary, omitting true member values.</summary>
    public static string SerializeDictionary(StructuredFieldDictionary dictionary)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        var output = new StringBuilder();
        foreach (var (key, member) in dictionary)
        {
            if (output.Length != 0)
            {
                output.Append(", ");
            }
            output.Append(key);
            if (member.IsItem && member.Item.Value is BooleanItem { BooleanValue: true })
            {
                WriteParameters(member.Parameters, output);
            }
            else
            {
                output.Append('=');
                WriteMember(member, output);
            }
        }
        return output.ToString();
    }

    private static void WriteItem(StructuredFieldItem item, StringBuilder output)
    {
        WriteBareItem(item.Value, output);
        WriteParameters(item.Parameters, output);
    }

    private static void WriteMember(StructuredFieldMember member, StringBuilder output)
    {
        if (member.IsItem)
        {
            WriteItem(member.Item, output);
        }
        else
        {
            WriteInnerList(member.InnerList, output);
        }
    }

    private static void WriteInnerList(InnerList innerList, StringBuilder output)
    {
        output.Append('(');
        for (var i = 0; i < innerList.Count; i++)
        {
            if (i != 0)
            {
                output.Append(' ');
            }
            WriteItem(innerList[i], output);
        }
        output.Append(')');
        WriteParameters(innerList.Parameters, output);
    }

    private static void WriteParameters(Parameters parameters, StringBuilder output)
    {
        foreach (var (key, value) in parameters)
        {
            output.Append(';').Append(key);
            if (value is not BooleanItem { BooleanValue: true })
            {
                output.Append('=');
                WriteBareItem(value, output);
            }
        }
    }

    private static void WriteBareItem(BareItem value, StringBuilder output)
    {
        switch (value)
        {
            case IntegerItem integer:
                output.Append(integer.LongValue.ToString(CultureInfo.InvariantCulture));
                break;
            case DecimalItem number:
                output.Append((number.DecimalValue == 0 ? 0m : number.DecimalValue)
                    .ToString("0.0##", CultureInfo.InvariantCulture));
                break;
            case StringItem text:
                output.Append('"');
                foreach (var c in text.StringValue)
                {
                    if (c is '"' or '\\')
                    {
                        output.Append('\\');
                    }
                    output.Append(c);
                }
                output.Append('"');
                break;
            case TokenItem token:
                output.Append(token.TokenValue);
                break;
            case ByteSequenceItem bytes:
                output.Append(':').Append(bytes.Base64Value).Append(':');
                break;
            case BooleanItem boolean:
                output.Append(boolean.BooleanValue ? "?1" : "?0");
                break;
            case DateItem date:
                output.Append('@').Append(date.UnixSeconds.ToString(CultureInfo.InvariantCulture));
                break;
            case DisplayStringItem display:
                WriteDisplayString(display.StringValue, output);
                break;
            default:
                throw new InvalidOperationException($"Unsupported bare value type: {value.GetType().Name}");
        }
    }

    private static void WriteDisplayString(string value, StringBuilder output)
    {
        const string hex = "0123456789abcdef";
        output.Append("%\"");
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            if (b is < 0x20 or > 0x7e or (byte)'%' or (byte)'"')
            {
                output.Append('%').Append(hex[b >> 4]).Append(hex[b & 0xf]);
            }
            else
            {
                output.Append((char)b);
            }
        }
        output.Append('"');
    }
}
