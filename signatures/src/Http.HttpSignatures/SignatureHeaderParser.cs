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

        var dictionary = new StructuredFieldDictionary();
        dictionary.Add(label, new ByteSequenceItem(signatureBytes));
        return StructuredFieldSerializer.SerializeDictionary(dictionary);
    }
}
