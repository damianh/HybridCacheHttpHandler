// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Constructs the signature base string per RFC 9421 §2.5.
/// The signature base is an ASCII string consisting of covered component values
/// followed by the <c>@signature-params</c> line.
/// </summary>
public static class SignatureBaseBuilder
{
    /// <summary>
    /// Creates the signature base as a byte array (UTF-8 encoding of ASCII string).
    /// </summary>
    /// <param name="parameters">The signature parameters defining covered components and metadata.</param>
    /// <param name="context">The HTTP message context to resolve component values from.</param>
    /// <param name="fieldTypeResolver">
    /// Declares the Structured Field type of HTTP fields, used to resolve <c>sf</c> and <c>key</c>
    /// components. When null, every field's type is treated as unknown, so <c>sf</c>/<c>key</c>
    /// components fail explicitly instead of guessing the type.
    /// </param>
    /// <returns>The signature base as a byte array.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters or context is null.</exception>
    /// <exception cref="SignatureBaseException">
    /// Thrown when the signature base cannot be constructed (missing component, invalid parameter, duplicate identifier, etc.).
    /// </exception>
    public static byte[] Build(
        SignatureParameters parameters,
        IHttpMessageContext context,
        IStructuredFieldTypeResolver? fieldTypeResolver = null)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(context);

        var str = BuildString(parameters, context, fieldTypeResolver);

        // BuildString already guarantees every character is printable ASCII or the line-feed
        // separator, so this can never silently substitute '?' for an unrepresentable character.
        return Encoding.ASCII.GetBytes(str);
    }

    /// <summary>
    /// Creates the signature base as a string (for debugging and testing).
    /// </summary>
    /// <param name="parameters">The signature parameters defining covered components and metadata.</param>
    /// <param name="context">The HTTP message context to resolve component values from.</param>
    /// <param name="fieldTypeResolver">
    /// Declares the Structured Field type of HTTP fields, used to resolve <c>sf</c> and <c>key</c>
    /// components. When null, every field's type is treated as unknown, so <c>sf</c>/<c>key</c>
    /// components fail explicitly instead of guessing the type.
    /// </param>
    /// <returns>The signature base string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when parameters or context is null.</exception>
    /// <exception cref="SignatureBaseException">
    /// Thrown when the signature base cannot be constructed.
    /// </exception>
    public static string BuildString(
        SignatureParameters parameters,
        IHttpMessageContext context,
        IStructuredFieldTypeResolver? fieldTypeResolver = null)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(context);

        var resolver = fieldTypeResolver ?? UnknownStructuredFieldTypeResolver.Instance;
        var sb = new StringBuilder();

        // RFC 9421 §2.5: component identifiers MUST NOT appear more than once, regardless of
        // wire parameter order, so identity uses ComponentIdentifier's order-independent equality.
        var seen = new HashSet<ComponentIdentifier>();

        // RFC 9421 §2.5: For each covered component in order
        foreach (var component in parameters.CoveredComponents)
        {
            if (!seen.Add(component))
            {
                throw new SignatureBaseException(
                    component,
                    "Duplicate component identifier in covered components list.");
            }

            // Resolve the component value
            string value;
            try
            {
                if (component.IsDerived)
                {
                    value = DerivedComponentResolver.Resolve(component, context);
                }
                else
                {
                    value = FieldComponentResolver.Resolve(component, context, resolver);
                }
            }
            catch (FormatException ex)
            {
                throw new SignatureBaseException(
                    component,
                    $"Component value is malformed: {ex.Message}",
                    ex);
            }

            ValidateAsciiSafe(component, value);

            // RFC 9421 §2.5: each line is: "component-id": value\n
            sb.Append(component.Serialize());
            sb.Append(": ");
            sb.Append(value);
            sb.Append('\n');
        }

        // RFC 9421 §2.5: final line is the @signature-params component
        // The value is the serialized signature parameters (Inner List form)
        sb.Append("\"@signature-params\": ");
        sb.Append(parameters.Serialize());

        // No trailing newline after the @signature-params line (per RFC 9421 §2.5)

        return sb.ToString();
    }

    /// <summary>
    /// Validates that a resolved component value contains only printable ASCII characters
    /// (0x20-0x7E). Characters outside this range - including the 0x0A line-feed used as the
    /// signature-base line separator - are rejected, so that <see cref="Build"/> never has to
    /// silently replace an unrepresentable character with '?' or allow a value to inject an
    /// extra line into the signature base.
    /// </summary>
    private static void ValidateAsciiSafe(ComponentIdentifier component, string value)
    {
        foreach (var c in value)
        {
            if (c >= 0x20 && c <= 0x7E)
                continue;

            throw new SignatureBaseException(
                component,
                $"Resolved component value contains a character (0x{(int)c:X4}) that cannot be " +
                "represented in the ASCII signature base.");
        }
    }
}
