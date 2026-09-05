// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Validates a component identifier's parameters before any resolver attempts to resolve its
/// value, per RFC 9421 §2.5. Shared by <see cref="DerivedComponentResolver"/> and
/// <see cref="FieldComponentResolver"/> so both local signing and incoming verification apply
/// the exact same strict rules.
/// </summary>
internal static class ComponentValidator
{
    private static readonly HashSet<string> KnownParameterNames =
        new(StringComparer.Ordinal) { "sf", "key", "bs", "req", "tr", "name" };

    /// <summary>
    /// Validates a component identifier's parameters for structural correctness.
    /// </summary>
    /// <param name="identifier">The component identifier to validate.</param>
    /// <param name="originalContext">
    /// The context originally passed to the signature-base builder, before any <c>req</c> redirection.
    /// Used to reject <c>req</c> on a request message even when an associated-request object happens
    /// to be populated.
    /// </param>
    /// <exception cref="SignatureBaseException">Thrown when the component identifier is invalid.</exception>
    internal static void Validate(ComponentIdentifier identifier, IHttpMessageContext originalContext)
    {
        if (identifier.Name == "@signature-params")
        {
            throw new SignatureBaseException(
                identifier,
                "The '@signature-params' component cannot be explicitly covered.");
        }

        foreach (var (parameterName, _) in identifier.Parameters)
        {
            if (!KnownParameterNames.Contains(parameterName))
            {
                throw new SignatureBaseException(
                    identifier,
                    $"Unsupported component parameter '{parameterName}'.");
            }
        }

        if (identifier.Bs && (identifier.Sf || identifier.Key is not null))
        {
            throw new SignatureBaseException(
                identifier,
                "The 'bs' parameter cannot be combined with 'sf' or 'key'.");
        }

        if (identifier.IsDerived)
        {
            if (identifier.Sf || identifier.Key is not null || identifier.Bs || identifier.Tr)
            {
                throw new SignatureBaseException(
                    identifier,
                    "'sf', 'key', 'bs', and 'tr' are not valid on derived components.");
            }

            if (identifier.Name == "@query-param")
            {
                if (identifier.QueryParamName is null)
                {
                    throw new SignatureBaseException(
                        identifier,
                        "'@query-param' requires a 'name' parameter.");
                }
            }
            else if (identifier.QueryParamName is not null)
            {
                throw new SignatureBaseException(
                    identifier,
                    "The 'name' parameter is only valid for '@query-param'.");
            }
        }
        else if (identifier.QueryParamName is not null)
        {
            throw new SignatureBaseException(
                identifier,
                "The 'name' parameter is only valid for '@query-param'.");
        }

        // Reject 'req' on a request message even if an associated-request object happens to be
        // populated: checked against the context as originally supplied, before any redirection.
        if (identifier.Req && originalContext.IsRequest)
        {
            throw new SignatureBaseException(
                identifier,
                "The 'req' parameter is not valid on a request message.");
        }
    }
}
