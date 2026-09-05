// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using DamianH.Http.StructuredFieldValues;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Parses and serializes <c>Signature-Input</c> and <c>Signature</c> HTTP fields
/// using Structured Field Values (RFC 8941) Dictionary format.
/// </summary>
public static class SignatureHeaderParser
{
    /// <summary>
    /// Parses a <c>Signature-Input</c> header value into labeled <see cref="SignatureParameters"/>.
    /// Each dictionary member is an Inner List of covered component identifiers with parameters.
    /// </summary>
    /// <param name="headerValue">The raw <c>Signature-Input</c> header value.</param>
    /// <returns>A dictionary mapping signature labels to parsed parameters.</returns>
    public static IReadOnlyDictionary<string, SignatureParameters> ParseSignatureInput(string headerValue)
    {
        ArgumentException.ThrowIfNullOrEmpty(headerValue);

        var dict = StructuredFieldParser.ParseDictionary(headerValue);
        var result = new Dictionary<string, SignatureParameters>(dict.Count);

        foreach (var member in dict)
        {
            if (!member.Value.IsInnerList)
                throw new FormatException(
                    $"Signature-Input member '{member.Key}' must be an Inner List.");

            result[member.Key] = SignatureParameters.Parse(member.Value.InnerList);
        }

        return result;
    }

    internal static SignatureParameters? ParseSignatureInput(string headerValue, string label)
    {
        ArgumentException.ThrowIfNullOrEmpty(headerValue);
        ValidateLabel(label);

        var dictionary = StructuredFieldParser.ParseDictionary(headerValue);
        if (!dictionary.TryGetValue(label, out var member))
            return null;

        if (!member.IsInnerList)
            throw new FormatException($"Signature-Input member '{label}' must be an Inner List.");

        return SignatureParameters.Parse(member.InnerList);
    }

    /// <summary>
    /// Parses a <c>Signature</c> header value into labeled signature byte arrays.
    /// Each dictionary member is a Byte Sequence containing the raw signature bytes.
    /// </summary>
    /// <param name="headerValue">The raw <c>Signature</c> header value.</param>
    /// <returns>A dictionary mapping signature labels to signature byte arrays.</returns>
    public static IReadOnlyDictionary<string, byte[]> ParseSignature(string headerValue)
    {
        ArgumentException.ThrowIfNullOrEmpty(headerValue);

        var dict = StructuredFieldParser.ParseDictionary(headerValue);
        var result = new Dictionary<string, byte[]>(dict.Count);

        foreach (var member in dict)
        {
            if (!member.Value.IsItem || member.Value.Item.Value is not ByteSequenceItem bsi)
                throw new FormatException(
                    $"Signature member '{member.Key}' must be a Byte Sequence item.");

            result[member.Key] = bsi.ToArray();
        }

        return result;
    }

    internal static byte[]? ParseSignature(string headerValue, string label)
    {
        ArgumentException.ThrowIfNullOrEmpty(headerValue);
        ValidateLabel(label);

        var dictionary = StructuredFieldParser.ParseDictionary(headerValue);
        if (!dictionary.TryGetValue(label, out var member))
            return null;

        if (!member.IsItem || member.Item.Value is not ByteSequenceItem byteSequence)
            throw new FormatException($"Signature member '{label}' must be a Byte Sequence item.");

        return byteSequence.ToArray();
    }

    /// <summary>
    /// Serializes a labeled <see cref="SignatureParameters"/> to a <c>Signature-Input</c> dictionary member.
    /// </summary>
    /// <param name="label">The signature label.</param>
    /// <param name="parameters">The signature parameters to serialize.</param>
    /// <returns>The serialized dictionary member, e.g. <c>sig1=("@method" "@authority");created=1618884473</c>.</returns>
    public static string SerializeSignatureInput(string label, SignatureParameters parameters)
    {
        ArgumentException.ThrowIfNullOrEmpty(label);
        ArgumentNullException.ThrowIfNull(parameters);
        ValidateLabel(label);

        return $"{label}={parameters.Serialize()}";
    }

    /// <summary>
    /// Serializes a labeled signature to a <c>Signature</c> dictionary member.
    /// </summary>
    /// <param name="label">The signature label.</param>
    /// <param name="signatureBytes">The raw signature bytes.</param>
    /// <returns>The serialized dictionary member, e.g. <c>sig1=:base64bytes:</c>.</returns>
    public static string SerializeSignature(string label, byte[] signatureBytes)
    {
        ArgumentException.ThrowIfNullOrEmpty(label);
        ArgumentNullException.ThrowIfNull(signatureBytes);
        ValidateLabel(label);

        var dictionary = new StructuredFieldDictionary();
        dictionary.Add(label, new ByteSequenceItem(signatureBytes));
        return StructuredFieldSerializer.SerializeDictionary(dictionary);
    }

    /// <summary>
    /// Validates that a signature label is a valid RFC 9651 dictionary key, so it can never
    /// corrupt the surrounding Structured Field wire format when interpolated.
    /// </summary>
    /// <param name="label">The signature label to validate.</param>
    /// <exception cref="ArgumentException">Thrown when the label is not a valid key.</exception>
    internal static void ValidateLabel(string label)
    {
        if (!TokenItem.IsValidKey(label))
        {
            throw new ArgumentException(
                $"Signature label '{label}' is not a valid RFC 9651 key. " +
                "Labels must start with a lowercase letter or '*' and contain only " +
                "lowercase letters, digits, '_', '-', '.', or '*'.",
                nameof(label));
        }
    }
}
