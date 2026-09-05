// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues;

/// <summary>
/// Represents a token item in a structured field value.
/// RFC 8941 defines tokens as unquoted identifiers following specific syntax rules.
/// Tokens start with an ASCII letter or '*' and continue with HTTP tchar, ':' or '/'.
/// </summary>
public sealed class TokenItem : BareItem
{
    private readonly string _value;

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenItem"/> class.
    /// </summary>
    /// <param name="value">The token value.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when value is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when value doesn't match RFC 8941 token syntax.
    /// </exception>
    public TokenItem(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrEmpty(value);
        ValidateToken(value);
        _value = value;
    }

    /// <summary>
    /// Gets the token value.
    /// </summary>
    public string TokenValue => _value;

    /// <inheritdoc/>
    public override object Value => _value;

    /// <inheritdoc/>
    public override ItemType Type => ItemType.Token;

    /// <inheritdoc/>
    public override string ToString() => _value;

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is TokenItem other && _value == other._value;

    /// <inheritdoc/>
    public override int GetHashCode() => _value.GetHashCode();

    /// <summary>
    /// Implicit conversion from string to TokenItem.
    /// </summary>
    public static implicit operator TokenItem(string value) => new(value);

    /// <summary>
    /// Implicit conversion from TokenItem to string.
    /// </summary>
    public static implicit operator string(TokenItem item) => item._value;

    /// <summary>
    /// Validates whether a string is a valid RFC 8941 token.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool IsValidToken(string value)
    {
        if (string.IsNullOrEmpty(value) || !IsTokenStart(value[0]))
        {
            return false;
        }
        foreach (var c in value.AsSpan(1))
        {
            if (!IsTokenCharacter(c))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Validates whether a string is a valid RFC 8941 key (for dictionary member keys and parameter keys).
    /// Keys use a more restrictive grammar than tokens:
    /// <c>sf-key = ( lcalpha / "*" ) *( lcalpha / DIGIT / "_" / "-" / "." / "*" )</c>
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool IsValidKey(string value)
    {
        if (string.IsNullOrEmpty(value) || !IsKeyStart(value[0]))
        {
            return false;
        }
        foreach (var c in value.AsSpan(1))
        {
            if (!IsKeyCharacter(c))
            {
                return false;
            }
        }
        return true;
    }

    internal static bool IsTokenStart(char c) => char.IsAsciiLetter(c) || c == '*';

    internal static bool IsTokenCharacter(char c) =>
        char.IsAsciiLetterOrDigit(c) || c is '!' or '#' or '$' or '%' or '&' or '\'' or '*'
            or '+' or '-' or '.' or '^' or '_' or '`' or '|' or '~' or ':' or '/';

    internal static bool IsKeyStart(char c) => c is >= 'a' and <= 'z' or '*';

    internal static bool IsKeyCharacter(char c) => IsKeyStart(c) || char.IsAsciiDigit(c) || c is '_' or '-' or '.';

    private static void ValidateToken(string value)
    {
        if (!IsValidToken(value))
        {
            throw new ArgumentException(
                $"Invalid token: '{value}'. Tokens must start with ASCII alpha or '*' " +
                "and contain only HTTP tchar, ':' or '/'.",
                nameof(value));
        }
    }
}
