// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;
using DamianH.Http.StructuredFieldValues;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Resolves HTTP field component values from an <see cref="IHttpMessageContext"/>.
/// Handles the <c>sf</c>, <c>key</c>, <c>bs</c>, <c>req</c>, and <c>tr</c> parameters.
/// RFC 9421 §2.1
/// </summary>
internal static class FieldComponentResolver
{
    /// <summary>
    /// Resolves the value of a field component from the given message context.
    /// </summary>
    /// <param name="identifier">The component identifier (must not be derived).</param>
    /// <param name="context">The HTTP message context to resolve from.</param>
    /// <param name="fieldTypeResolver">
    /// Declares the Structured Field type of HTTP fields, required for <c>sf</c> and <c>key</c>.
    /// </param>
    /// <returns>The component value string.</returns>
    /// <exception cref="SignatureBaseException">Thrown when the component cannot be resolved.</exception>
    internal static string Resolve(
        ComponentIdentifier identifier,
        IHttpMessageContext context,
        IStructuredFieldTypeResolver fieldTypeResolver)
    {
        ComponentValidator.Validate(identifier, context);

        // 'req' redirects resolution to the associated request context (RFC 9421 §2.4)
        var resolveContext = identifier.Req
            ? context.AssociatedRequest ?? throw new SignatureBaseException(
                identifier,
                "Component has 'req' parameter but no associated request is available.")
            : context;

        // 'bs' — binary-wrapped encoding (RFC 9421 §2.1.3)
        if (identifier.Bs)
            return ResolveBinaryWrapped(identifier, resolveContext);

        // 'key' — dictionary member extraction (RFC 9421 §2.1.2)
        if (identifier.Key is not null)
            return ResolveDictionaryKey(identifier, resolveContext, fieldTypeResolver);

        // 'sf' — strict structured field serialization (RFC 9421 §2.1.1)
        if (identifier.Sf)
            return ResolveStrictSf(identifier, resolveContext, fieldTypeResolver);

        // Default: combined field value (RFC 9421 §2.1, RFC 9110 §5.2)
        return ResolveCombined(identifier, resolveContext);
    }

    private static string? GetRawValue(ComponentIdentifier identifier, IHttpMessageContext context)
    {
        try
        {
            return identifier.Tr
                ? context.GetTrailerValue(identifier.Name)
                : context.GetHeaderValue(identifier.Name);
        }
        catch (FormatException ex)
        {
            var section = identifier.Tr ? "trailer" : "header";
            throw new SignatureBaseException(
                identifier,
                $"The {section} field '{identifier.Name}' could not be read: {ex.Message}",
                ex);
        }
    }

    private static string ResolveCombined(ComponentIdentifier identifier, IHttpMessageContext context)
    {
        var value = GetRawValue(identifier, context);
        if (value is null)
        {
            var section = identifier.Tr ? "Trailer" : "Header";
            throw new SignatureBaseException(
                identifier,
                $"{section} field '{identifier.Name}' is not present in the message.");
        }

        return value;
    }

    private static StructuredFieldValueKind ResolveDeclaredType(
        ComponentIdentifier identifier,
        IHttpMessageContext context,
        IStructuredFieldTypeResolver fieldTypeResolver)
    {
        var kind = fieldTypeResolver.ResolveType(context.IsRequest, identifier.Name);
        if (kind == StructuredFieldValueKind.Unknown)
        {
            throw new SignatureBaseException(
                identifier,
                $"Field '{identifier.Name}' has no declared Structured Field type; " +
                "'sf' and 'key' processing requires the type to be known in advance rather than guessed.");
        }

        return kind;
    }

    private static string ResolveStrictSf(
        ComponentIdentifier identifier,
        IHttpMessageContext context,
        IStructuredFieldTypeResolver fieldTypeResolver)
    {
        var rawValue = ResolveCombined(identifier, context);
        var kind = ResolveDeclaredType(identifier, context, fieldTypeResolver);

        try
        {
            return kind switch
            {
                StructuredFieldValueKind.Dictionary =>
                    StructuredFieldSerializer.SerializeDictionary(StructuredFieldParser.ParseDictionary(rawValue)),
                StructuredFieldValueKind.List =>
                    StructuredFieldSerializer.SerializeList(StructuredFieldParser.ParseList(rawValue)),
                StructuredFieldValueKind.Item =>
                    StructuredFieldSerializer.SerializeItem(StructuredFieldParser.ParseItem(rawValue)),
                _ => throw new SignatureBaseException(identifier, $"Unsupported declared Structured Field type '{kind}'."),
            };
        }
        catch (StructuredFieldParseException ex)
        {
            throw new SignatureBaseException(
                identifier,
                $"Field '{identifier.Name}' could not be parsed as the declared Structured Field type '{kind}'.",
                ex);
        }
    }

    private static string ResolveDictionaryKey(
        ComponentIdentifier identifier,
        IHttpMessageContext context,
        IStructuredFieldTypeResolver fieldTypeResolver)
    {
        var rawValue = ResolveCombined(identifier, context);
        var kind = ResolveDeclaredType(identifier, context, fieldTypeResolver);
        if (kind != StructuredFieldValueKind.Dictionary)
        {
            throw new SignatureBaseException(
                identifier,
                $"Field '{identifier.Name}' is declared as '{kind}', not a Structured Field Dictionary; " +
                "the 'key' parameter requires a Dictionary field.");
        }

        StructuredFieldDictionary dict;
        try
        {
            dict = StructuredFieldParser.ParseDictionary(rawValue);
        }
        catch (StructuredFieldParseException ex)
        {
            throw new SignatureBaseException(
                identifier,
                $"Field '{identifier.Name}' with 'key' parameter could not be parsed as SF Dictionary.",
                ex);
        }

        var key = identifier.Key!;
        if (!dict.TryGetValue(key, out var member))
            throw new SignatureBaseException(
                identifier,
                $"SF Dictionary key '{key}' not found in '{identifier.Name}' field.");

        return StructuredFieldSerializer.SerializeMember(member);
    }

    private static string ResolveBinaryWrapped(ComponentIdentifier identifier, IHttpMessageContext context)
    {
        var values = identifier.Tr ? context.GetTrailerValues(identifier.Name) : context.GetHeaderValues(identifier.Name);
        if (values.Count == 0)
        {
            var section = identifier.Tr ? "Trailer" : "Header";
            throw new SignatureBaseException(
                identifier,
                $"{section} field '{identifier.Name}' is not present in the message.");
        }

        // RFC 9421 §2.1.3: each raw field value is wrapped as an SF Byte Sequence,
        // then the byte sequences are combined with ", "
        var sb = new StringBuilder();
        for (var i = 0; i < values.Count; i++)
        {
            if (i > 0) sb.Append(", ");

            var section = identifier.Tr ? "trailer" : "header";
            var canonicalValue = HttpFieldValueCanonicalizer.CanonicalizeSingle(
                identifier.Name,
                section,
                values[i]);
            var bytes = EncodeLatin1Lossless(identifier, canonicalValue);
            var item = new ByteSequenceItem(bytes);
            sb.Append(StructuredFieldSerializer.SerializeBareItem(item));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Encodes a raw field value as Latin-1 bytes for binary-wrapped (<c>bs</c>) processing,
    /// rejecting any character outside the Latin-1 range (U+0000-U+00FF) rather than silently
    /// replacing it with '?', since such a replacement would make the wrapped bytes lossy and
    /// unable to reproduce the original field value.
    /// </summary>
    private static byte[] EncodeLatin1Lossless(ComponentIdentifier identifier, string value)
    {
        var bytes = new byte[value.Length];
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c > 0xFF)
            {
                throw new SignatureBaseException(
                    identifier,
                    $"Field value contains character U+{(int)c:X4}, which cannot be represented " +
                    "losslessly as a single Latin-1 byte for 'bs' binary-wrapped encoding.");
            }

            bytes[i] = (byte)c;
        }

        return bytes;
    }
}
