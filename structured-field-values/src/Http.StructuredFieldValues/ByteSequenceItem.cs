// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues;

/// <summary>
/// Represents a byte sequence item in a structured field value.
/// RFC 8941 encodes byte sequences as base64 between colons (e.g., :aGVsbG8=:).
/// </summary>
public sealed class ByteSequenceItem : BareItem
{
    private readonly byte[] _value;

    /// <summary>
    /// Initializes a new instance of the <see cref="ByteSequenceItem"/> class.
    /// </summary>
    /// <param name="value">The byte array value.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when value is null.
    /// </exception>
    public ByteSequenceItem(byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = (byte[])value.Clone();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ByteSequenceItem"/> class from a base64 string.
    /// </summary>
    /// <param name="base64Value">The base64-encoded value.</param>
    /// <returns>A new ByteSequenceItem.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when base64Value is null.
    /// </exception>
    /// <exception cref="FormatException">
    /// Thrown when base64Value is not valid base64.
    /// </exception>
    public static ByteSequenceItem FromBase64(string base64Value)
    {
        ArgumentNullException.ThrowIfNull(base64Value);
        var bytes = Convert.FromBase64String(base64Value);
        return new ByteSequenceItem(bytes);
    }

    /// <summary>
    /// Gets a defensive copy of the byte array value.
    /// </summary>
    public byte[] ByteArrayValue => ToArray();

    /// <summary>Gets read-only access to the immutable bytes.</summary>
    public ReadOnlySpan<byte> Bytes => _value;

    /// <summary>Returns a new copy of the bytes.</summary>
    public byte[] ToArray() => (byte[])_value.Clone();

    /// <summary>
    /// Gets the base64-encoded representation of the byte sequence.
    /// </summary>
    public string Base64Value => Convert.ToBase64String(_value);

    /// <inheritdoc/>
    public override object Value => ToArray();

    /// <inheritdoc/>
    public override ItemType Type => ItemType.ByteSequence;

    /// <inheritdoc/>
    public override string ToString() => $":{Base64Value}:";

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is ByteSequenceItem other && _value.SequenceEqual(other._value);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var b in _value)
        {
            hash.Add(b);
        }
        return hash.ToHashCode();
    }

}
