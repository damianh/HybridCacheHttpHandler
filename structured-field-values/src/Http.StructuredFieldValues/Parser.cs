// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues;

/// <summary>
/// Internal helper for parsing structured field values according to RFC 9651.
/// Maintains parsing state and provides low-level parsing primitives.
/// </summary>
internal ref struct Parser(ReadOnlySpan<char> input)
{
    private readonly ReadOnlySpan<char> _input = input;
    private int _position = 0;

    public int Position => _position;
    public bool IsAtEnd => _position >= _input.Length;
    public char Current => IsAtEnd ? '\0' : _input[_position];
    public ReadOnlySpan<char> Remaining => IsAtEnd ? ReadOnlySpan<char>.Empty : _input[_position..];

    /// <summary>
    /// Advances the position by one character.
    /// </summary>
    public void Advance()
    {
        if (!IsAtEnd)
        {
            _position++;
        }
    }

    /// <summary>
    /// Advances the position by the specified count.
    /// </summary>
    public void Advance(int count)
        => _position = Math.Min(_position + count, _input.Length);

    /// <summary>
    /// Peeks at the character at the specified offset from current position.
    /// </summary>
    public char Peek(int offset = 1)
    {
        var pos = _position + offset;
        return pos < _input.Length ? _input[pos] : '\0';
    }

    /// <summary>
    /// Consumes the current character if it matches the expected character.
    /// </summary>
    public bool TryConsume(char expected)
    {
        if (Current == expected)
        {
            Advance();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Consumes optional whitespace (SP only, 0x20).
    /// Used for item parsing where tabs are not allowed in leading/trailing position.
    /// </summary>
    public void ConsumeOptionalSpaces()
    {
        while (!IsAtEnd && Current == ' ')
        {
            Advance();
        }
    }

    /// <summary>
    /// Consumes optional whitespace (SP / HTAB).
    /// RFC 8941 § 3.1: OWS = *( SP / HTAB )
    /// Used between list/dictionary members.
    /// </summary>
    public void ConsumeOptionalWhitespace()
    {
        while (!IsAtEnd && Current is ' ' or '\t')
        {
            Advance();
        }
    }

    /// <summary>
    /// Throws a parse exception with the current position.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public void ThrowParseException(string message) => throw new StructuredFieldParseException(message, _position);

    /// <summary>
    /// Checks if the current character is a digit (0-9).
    /// </summary>
    public bool IsDigit() => Current >= '0' && Current <= '9';

    /// <summary>
    /// Checks if the current character is an alpha character (A-Z, a-z).
    /// </summary>
    public bool IsAlpha() => (Current >= 'A' && Current <= 'Z') || (Current >= 'a' && Current <= 'z');

    /// <summary>
    /// Checks if the character is valid for tokens.
    /// RFC 9651: tchar / ":" / "/".
    /// </summary>
    public bool IsTokenChar() => TokenItem.IsTokenCharacter(Current);

    /// <summary>
    /// Checks if the character is a valid key character.
    /// RFC 8941: lcalpha / DIGIT / "_" / "-" / "." / "*"
    /// </summary>
    public bool IsKeyChar() => TokenItem.IsKeyCharacter(Current);
}
