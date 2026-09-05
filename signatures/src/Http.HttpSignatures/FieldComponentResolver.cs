// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;
using DamianH.Http.StructuredFieldValues;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Resolves HTTP field component values from an <see cref="IHttpMessageContext"/>.
/// Handles the <c>sf</c>, <c>key</c>, <c>bs</c>, and <c>req</c> parameters.
/// RFC 9421 §2.1
/// </summary>
internal static class FieldComponentResolver
{
    /// <summary>
    /// Resolves the value of a field component from the given message context.
    /// </summary>
    /// <param name="identifier">The component identifier (must not be derived).</param>
    /// <param name="context">The HTTP message context to resolve from.</param>
    /// <returns>The component value string.</returns>
    /// <exception cref="SignatureBaseException">Thrown when the component cannot be resolved.</exception>
    internal static string Resolve(ComponentIdentifier identifier, IHttpMessageContext context)
    {
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
            return ResolveDictionaryKey(identifier, resolveContext);

        // 'sf' — strict structured field serialization (RFC 9421 §2.1.1)
        if (identifier.Sf)
            return ResolveStrictSf(identifier, resolveContext);

        // Default: combined field value (RFC 9421 §2.1, RFC 9110 §5.2)
        return ResolveCombined(identifier, resolveContext);
    }

    private static string ResolveCombined(ComponentIdentifier identifier, IHttpMessageContext context)
    {
        var value = context.GetHeaderValue(identifier.Name);
        if (value is null)
            throw new SignatureBaseException(
                identifier,
                $"Header field '{identifier.Name}' is not present in the message.");
        return value;
    }

    private static string ResolveStrictSf(ComponentIdentifier identifier, IHttpMessageContext context)
    {
        var rawValue = context.GetHeaderValue(identifier.Name);
        if (rawValue is null)
            throw new SignatureBaseException(
                identifier,
                $"Header field '{identifier.Name}' is not present in the message.");

        // Try parsing as each SF type and re-serialize canonically
        // Attempt Dictionary first, then List, then Item
        try
        {
            var dict = StructuredFieldParser.ParseDictionary(rawValue);
            return StructuredFieldSerializer.SerializeDictionary(dict);
        }
        catch (StructuredFieldParseException) { }

        try
        {
            var list = StructuredFieldParser.ParseList(rawValue);
            return StructuredFieldSerializer.SerializeList(list);
        }
        catch (StructuredFieldParseException) { }

        try
        {
            var item = StructuredFieldParser.ParseItem(rawValue);
            return StructuredFieldSerializer.SerializeItem(item);
        }
        catch (StructuredFieldParseException ex)
        {
            throw new SignatureBaseException(
                identifier,
                $"Header field '{identifier.Name}' with 'sf' parameter could not be parsed as any SF type.",
                ex);
        }
    }

    private static string ResolveDictionaryKey(ComponentIdentifier identifier, IHttpMessageContext context)
    {
        var rawValue = context.GetHeaderValue(identifier.Name);
        if (rawValue is null)
            throw new SignatureBaseException(
                identifier,
                $"Header field '{identifier.Name}' is not present in the message.");

        StructuredFieldDictionary dict;
        try
        {
            dict = StructuredFieldParser.ParseDictionary(rawValue);
        }
        catch (StructuredFieldParseException ex)
        {
            throw new SignatureBaseException(
                identifier,
                $"Header field '{identifier.Name}' with 'key' parameter could not be parsed as SF Dictionary.",
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
        var values = context.GetHeaderValues(identifier.Name);
        if (values.Count == 0)
            throw new SignatureBaseException(
                identifier,
                $"Header field '{identifier.Name}' is not present in the message.");

        // RFC 9421 §2.1.3: each raw field value is wrapped as an SF Byte Sequence,
        // then the byte sequences are combined with ", "
        var sb = new StringBuilder();
        for (var i = 0; i < values.Count; i++)
        {
            if (i > 0) sb.Append(", ");

            var bytes = System.Text.Encoding.Latin1.GetBytes(values[i]);
            var item = new ByteSequenceItem(bytes);
            sb.Append(StructuredFieldSerializer.SerializeBareItem(item));
        }

        return sb.ToString();
    }
}
