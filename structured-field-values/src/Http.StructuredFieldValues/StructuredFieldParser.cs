// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;

namespace DamianH.Http.StructuredFieldValues;

/// <summary>Parses structured fields according to RFC 9651 (including RFC 8941 types).</summary>
/// <remarks>
/// Null input is programmer misuse and throws <see cref="ArgumentNullException"/>.
/// Invalid wire input throws <see cref="StructuredFieldParseException"/> with a position.
/// </remarks>
public static class StructuredFieldParser
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>Parses a complete bare value, without parameters.</summary>
    public static BareItem ParseBareItem(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var parser = new Parser(input.AsSpan());
        parser.ConsumeOptionalSpaces();
        var value = ParseBareItem(ref parser);
        RequireEnd(ref parser);
        return value;
    }

    /// <summary>Parses a complete item with its owned parameters.</summary>
    public static StructuredFieldItem ParseItem(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var parser = new Parser(input.AsSpan());
        parser.ConsumeOptionalSpaces();
        var item = ParseItem(ref parser);
        RequireEnd(ref parser);
        return item;
    }

    /// <summary>Parses a complete list. Empty input represents an empty list.</summary>
    public static StructuredFieldList ParseList(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var parser = new Parser(input.AsSpan());
        var list = new StructuredFieldList();
        parser.ConsumeOptionalSpaces();
        while (!parser.IsAtEnd)
        {
            list.Add(ParseMember(ref parser));
            if (!ConsumeSeparator(ref parser))
            {
                break;
            }
        }
        return list;
    }

    /// <summary>
    /// Parses a complete dictionary. Duplicate keys replace values in their original position.
    /// Empty input represents an empty dictionary.
    /// </summary>
    public static StructuredFieldDictionary ParseDictionary(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var parser = new Parser(input.AsSpan());
        var dictionary = new StructuredFieldDictionary();
        parser.ConsumeOptionalSpaces();
        while (!parser.IsAtEnd)
        {
            var key = ParseKey(ref parser);
            dictionary[key] = parser.TryConsume('=')
                ? ParseMember(ref parser)
                : StructuredFieldMember.FromItem(new StructuredFieldItem(BooleanItem.True, ParseParameters(ref parser)));
            if (!ConsumeSeparator(ref parser))
            {
                break;
            }
        }
        return dictionary;
    }

    private static void RequireEnd(ref Parser parser)
    {
        parser.ConsumeOptionalSpaces();
        if (!parser.IsAtEnd)
        {
            parser.ThrowParseException("Unexpected characters after item");
        }
    }

    private static bool ConsumeSeparator(ref Parser parser)
    {
        parser.ConsumeOptionalWhitespace();
        if (parser.IsAtEnd)
        {
            return false;
        }
        if (!parser.TryConsume(','))
        {
            parser.ThrowParseException("Expected ',' between members");
        }
        parser.ConsumeOptionalWhitespace();
        if (parser.IsAtEnd)
        {
            parser.ThrowParseException("Unexpected end of input after ','");
        }
        return true;
    }

    private static StructuredFieldMember ParseMember(ref Parser parser) =>
        parser.Current == '('
            ? StructuredFieldMember.FromInnerList(ParseInnerList(ref parser))
            : StructuredFieldMember.FromItem(ParseItem(ref parser));

    private static StructuredFieldItem ParseItem(ref Parser parser)
    {
        var value = ParseBareItem(ref parser);
        return new StructuredFieldItem(value, ParseParameters(ref parser));
    }

    private static InnerList ParseInnerList(ref Parser parser)
    {
        parser.Advance();
        var items = new List<StructuredFieldItem>();
        while (true)
        {
            parser.ConsumeOptionalSpaces();
            if (parser.TryConsume(')'))
            {
                return new InnerList(items, ParseParameters(ref parser));
            }
            items.Add(ParseItem(ref parser));
            if (parser.Current is not (' ' or ')'))
            {
                parser.ThrowParseException("Expected SP or ')' after inner list item");
            }
        }
    }

    private static Parameters ParseParameters(ref Parser parser)
    {
        var parameters = new Parameters();
        while (parser.TryConsume(';'))
        {
            parser.ConsumeOptionalSpaces();
            var key = ParseKey(ref parser);
            parameters[key] = parser.TryConsume('=') ? ParseBareItem(ref parser) : BooleanItem.True;
        }
        return parameters;
    }

    private static BareItem ParseBareItem(ref Parser parser)
    {
        if (parser.IsDigit() || parser.Current == '-')
        {
            return ParseNumber(ref parser);
        }
        if (TokenItem.IsTokenStart(parser.Current))
        {
            return ParseToken(ref parser);
        }
        switch (parser.Current)
        {
            case '"': return ParseString(ref parser);
            case ':': return ParseByteSequence(ref parser);
            case '?': return ParseBoolean(ref parser);
            case '@':
                parser.Advance();
                var number = ParseNumber(ref parser);
                if (number is not IntegerItem)
                {
                    parser.ThrowParseException("A date must contain integer Unix seconds");
                }
                return new DateItem(((IntegerItem)number).LongValue);
            case '%': return ParseDisplayString(ref parser);
            default:
                parser.ThrowParseException("Expected a bare item");
                return null!;
        }
    }

    private static BareItem ParseNumber(ref Parser parser)
    {
        var negative = parser.TryConsume('-');
        if (!parser.IsDigit())
        {
            parser.ThrowParseException("Expected a digit");
        }
        long integer = 0;
        var integerDigits = 0;
        while (parser.IsDigit())
        {
            if (++integerDigits > 15)
            {
                parser.ThrowParseException("An integer has at most 15 digits");
            }
            integer = integer * 10 + parser.Current - '0';
            parser.Advance();
        }
        if (!parser.TryConsume('.'))
        {
            return new IntegerItem(negative ? -integer : integer);
        }
        if (integerDigits > DecimalItem.MaxIntegerDigits)
        {
            parser.ThrowParseException("A decimal has at most 12 integer digits");
        }
        if (!parser.IsDigit())
        {
            parser.ThrowParseException("Expected a digit after '.'");
        }
        var fractionDigits = 0;
        var fraction = 0;
        var divisor = 1m;
        while (parser.IsDigit())
        {
            if (++fractionDigits > DecimalItem.MaxDecimalPlaces)
            {
                parser.ThrowParseException("A decimal has at most three fractional digits");
            }
            fraction = fraction * 10 + parser.Current - '0';
            divisor *= 10;
            parser.Advance();
        }
        var value = integer + fraction / divisor;
        return new DecimalItem(negative ? -value : value);
    }

    private static StringItem ParseString(ref Parser parser)
    {
        parser.Advance();
        var value = new StringBuilder();
        while (!parser.IsAtEnd)
        {
            if (parser.TryConsume('"'))
            {
                return new StringItem(value.ToString());
            }
            if (parser.TryConsume('\\'))
            {
                if (parser.IsAtEnd || parser.Current is not ('"' or '\\'))
                {
                    parser.ThrowParseException("Invalid string escape");
                }
            }
            else if (parser.Current is < (char)0x20 or > (char)0x7e)
            {
                parser.ThrowParseException("Strings require printable ASCII");
            }
            value.Append(parser.Current);
            parser.Advance();
        }
        parser.ThrowParseException("Unterminated string");
        return null!;
    }

    private static TokenItem ParseToken(ref Parser parser)
    {
        var value = new StringBuilder();
        while (parser.IsTokenChar())
        {
            value.Append(parser.Current);
            parser.Advance();
        }
        return new TokenItem(value.ToString());
    }

    private static ByteSequenceItem ParseByteSequence(ref Parser parser)
    {
        parser.Advance();
        var start = parser.Position;
        var value = new StringBuilder();
        while (!parser.TryConsume(':'))
        {
            if (parser.IsAtEnd)
            {
                parser.ThrowParseException("Unterminated byte sequence");
            }
            if (!char.IsAsciiLetterOrDigit(parser.Current) && parser.Current is not ('+' or '/' or '='))
            {
                parser.ThrowParseException("Invalid base64 character");
            }
            value.Append(parser.Current);
            parser.Advance();
        }
        while (value.Length % 4 != 0)
        {
            value.Append('=');
        }
        try
        {
            return ByteSequenceItem.FromBase64(value.ToString());
        }
        catch (FormatException ex)
        {
            throw new StructuredFieldParseException("Invalid base64 byte sequence", start, ex);
        }
    }

    private static BooleanItem ParseBoolean(ref Parser parser)
    {
        parser.Advance();
        if (parser.TryConsume('1'))
        {
            return BooleanItem.True;
        }
        if (parser.TryConsume('0'))
        {
            return BooleanItem.False;
        }
        parser.ThrowParseException("Expected '0' or '1' after '?'");
        return null!;
    }

    private static DisplayStringItem ParseDisplayString(ref Parser parser)
    {
        parser.Advance();
        if (!parser.TryConsume('"'))
        {
            parser.ThrowParseException("Expected a quote after '%'");
        }
        var start = parser.Position;
        var bytes = new List<byte>();
        while (!parser.IsAtEnd)
        {
            if (parser.TryConsume('"'))
            {
                try
                {
                    return new DisplayStringItem(StrictUtf8.GetString(bytes.ToArray()));
                }
                catch (DecoderFallbackException ex)
                {
                    throw new StructuredFieldParseException("Invalid UTF-8 display string", start, ex);
                }
            }
            if (parser.TryConsume('%'))
            {
                var high = LowerHex(parser.Current);
                var low = LowerHex(parser.Peek());
                if (high < 0 || low < 0)
                {
                    parser.ThrowParseException("Expected two lowercase hexadecimal digits after '%'");
                }
                bytes.Add((byte)(high * 16 + low));
                parser.Advance(2);
            }
            else
            {
                if (parser.Current is < (char)0x20 or > (char)0x7e)
                {
                    parser.ThrowParseException("Unescaped display string characters require printable ASCII");
                }
                bytes.Add((byte)parser.Current);
                parser.Advance();
            }
        }
        parser.ThrowParseException("Unterminated display string");
        return null!;
    }

    private static int LowerHex(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        _ => -1
    };

    private static string ParseKey(ref Parser parser)
    {
        if (!TokenItem.IsKeyStart(parser.Current))
        {
            parser.ThrowParseException("Key must start with a lowercase ASCII letter or '*'");
        }
        var key = new StringBuilder();
        while (parser.IsKeyChar())
        {
            key.Append(parser.Current);
            parser.Advance();
        }
        return key.ToString();
    }
}
