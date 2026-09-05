// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace DamianH.Http.ForwardedHeaders;

/// <summary>
/// Parses RFC 7239 Forwarded field syntax independently of node, host, and scheme semantics.
/// </summary>
public static class ForwardedHeaderParser
{
    /// <summary>Parses one Forwarded field value.</summary>
    /// <param name="value">The original field value.</param>
    /// <returns>The immutable parsed header.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="FormatException">The field syntax is invalid.</exception>
    public static ForwardedHeader Parse(string value)
    {
        if (!TryParse(value, out var header, out var error))
        {
            throw new FormatException($"{error.Message} (position {error.Position}).");
        }

        return header;
    }

    /// <summary>Parses ordered Forwarded field values as a single comma-joined field.</summary>
    /// <param name="values">The ordered field values. Null members are treated as empty field values.</param>
    /// <returns>The immutable parsed header.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is null.</exception>
    /// <exception cref="FormatException">The combined field syntax is invalid.</exception>
    public static ForwardedHeader Parse(IEnumerable<string?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Parse(string.Join(",", values));
    }

    /// <summary>Attempts to parse one Forwarded field value without throwing for malformed syntax.</summary>
    /// <param name="value">The original field value.</param>
    /// <param name="header">The parsed header on success; otherwise null.</param>
    /// <param name="error">The safe diagnostic on failure; otherwise null.</param>
    /// <returns>Whether the field syntax is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static bool TryParse(
        string value,
        [NotNullWhen(true)] out ForwardedHeader? header,
        [NotNullWhen(false)] out ForwardedParseError? error)
    {
        ArgumentNullException.ThrowIfNull(value);
        var parser = new Parser(value);
        return parser.TryParse(out header, out error);
    }

    /// <summary>Attempts to parse ordered field values as a single comma-joined field.</summary>
    /// <param name="values">The ordered field values. Null members are treated as empty field values.</param>
    /// <param name="header">The parsed header on success; otherwise null.</param>
    /// <param name="error">The safe diagnostic on failure; otherwise null.</param>
    /// <returns>Whether the combined field syntax is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is null.</exception>
    public static bool TryParse(
        IEnumerable<string?> values,
        [NotNullWhen(true)] out ForwardedHeader? header,
        [NotNullWhen(false)] out ForwardedParseError? error)
    {
        ArgumentNullException.ThrowIfNull(values);
        return TryParse(string.Join(",", values), out header, out error);
    }

    private sealed class Parser(string value)
    {
        private int _position;
        private ForwardedParseError? _error;

        public bool TryParse(
            [NotNullWhen(true)] out ForwardedHeader? header,
            [NotNullWhen(false)] out ForwardedParseError? error)
        {
            var elements = new List<ForwardedElement>();
            var precedingComma = 0;
            while (true)
            {
                SkipWhitespace();
                if (_position == value.Length)
                {
                    header = new ForwardedHeader(value, elements);
                    error = null;
                    return true;
                }

                if (value[_position] == ',')
                {
                    precedingComma = _position++;
                    continue;
                }

                var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (!ReadElement(parameters))
                {
                    header = null;
                    error = _error!;
                    return false;
                }

                elements.Add(new ForwardedElement(parameters, elements.Count == 0 ? 0 : precedingComma));
                SkipWhitespace();
                if (_position != value.Length && value[_position] != ',')
                {
                    header = null;
                    error = new ForwardedParseError(_position, "Expected a comma or the end of the field");
                    return false;
                }
            }
        }

        private bool ReadElement(Dictionary<string, string> parameters)
        {
            while (true)
            {
                if (_position == value.Length || value[_position] == ',' || IsWhitespace(value[_position]))
                {
                    return true;
                }

                if (value[_position] == ';')
                {
                    _position++;
                    continue;
                }

                var nameStart = _position;
                var name = ReadToken();
                if (name.Length == 0)
                {
                    return Fail("Expected a parameter name");
                }

                if (_position == value.Length || value[_position] != '=')
                {
                    return Fail("Expected an equals sign immediately after the parameter name");
                }

                _position++;
                string parameterValue;
                if (_position < value.Length && value[_position] == '"')
                {
                    if (!ReadQuotedString(out parameterValue))
                    {
                        return false;
                    }
                }
                else
                {
                    parameterValue = ReadToken();
                    if (parameterValue.Length == 0)
                    {
                        return Fail("Expected a token or quoted parameter value");
                    }
                }

                if (!parameters.TryAdd(name, parameterValue))
                {
                    _error = new ForwardedParseError(nameStart, "A parameter name occurs more than once in an element");
                    return false;
                }

                if (_position == value.Length || value[_position] != ';')
                {
                    return true;
                }

                _position++;
            }
        }

        private string ReadToken()
        {
            var start = _position;
            while (_position < value.Length && IsTokenCharacter(value[_position]))
            {
                _position++;
            }

            return value[start.._position];
        }

        private bool ReadQuotedString(out string result)
        {
            _position++;
            var start = _position;
            StringBuilder? decoded = null;
            while (_position < value.Length)
            {
                var character = value[_position];
                if (character == '"')
                {
                    result = decoded is null ? value[start.._position] : decoded.ToString();
                    _position++;
                    return true;
                }

                if (character == '\\')
                {
                    decoded ??= new StringBuilder().Append(value, start, _position - start);
                    _position++;
                    if (_position == value.Length)
                    {
                        result = string.Empty;
                        return Fail("Expected a quoted-pair character");
                    }

                    character = value[_position];
                    if (!IsQuotedPairCharacter(character))
                    {
                        result = string.Empty;
                        return Fail("Invalid quoted-pair character");
                    }

                    decoded.Append(character);
                }
                else
                {
                    if (!IsQuotedTextCharacter(character))
                    {
                        result = string.Empty;
                        return Fail("Invalid quoted-string character");
                    }

                    decoded?.Append(character);
                }

                _position++;
            }

            result = string.Empty;
            return Fail("Expected a closing quote");
        }

        private void SkipWhitespace()
        {
            while (_position < value.Length && IsWhitespace(value[_position]))
            {
                _position++;
            }
        }

        private bool Fail(string message)
        {
            _error = new ForwardedParseError(_position, message);
            return false;
        }

        private static bool IsWhitespace(char character) => character is ' ' or '\t';

        private static bool IsTokenCharacter(char character) =>
            character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9'
                or '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '-' or '.'
                or '^' or '_' or '`' or '|' or '~';

        private static bool IsQuotedTextCharacter(char character) =>
            character is '\t' or ' ' or '!' or >= '#' and <= '[' or >= ']' and <= '~'
                or >= '\u0080' and <= '\u00ff';

        private static bool IsQuotedPairCharacter(char character) =>
            character is '\t' or >= ' ' and <= '~' or >= '\u0080' and <= '\u00ff';
    }
}
