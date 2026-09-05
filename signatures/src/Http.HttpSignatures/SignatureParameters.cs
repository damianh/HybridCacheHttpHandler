// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using DamianH.Http.StructuredFieldValues;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Represents the signature parameters per RFC 9421 §2.3.
/// Contains the ordered set of covered components and signature metadata (created, expires, nonce, alg, keyid, tag).
/// </summary>
public sealed class SignatureParameters
{
    /// <summary>
    /// The exact wire metadata parameters as parsed, preserving order and any unknown parameters,
    /// for full-fidelity round-trip serialization. Null for locally constructed instances, which
    /// instead serialize the typed properties in RFC 9421 canonical order.
    /// </summary>
    private readonly Parameters? _wireParameters;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignatureParameters"/> class.
    /// </summary>
    /// <param name="coveredComponents">The ordered list of component identifiers to cover.</param>
    public SignatureParameters(IReadOnlyList<ComponentIdentifier> coveredComponents)
    {
        ArgumentNullException.ThrowIfNull(coveredComponents);

        // Defensive copy: a caller-supplied list must not be able to change this instance afterward.
        CoveredComponents = Array.AsReadOnly(coveredComponents.ToArray());
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SignatureParameters"/> class from parsed wire data,
    /// preserving the exact metadata parameter order (including unknown parameters) for round-trip fidelity.
    /// </summary>
    private SignatureParameters(
        IReadOnlyList<ComponentIdentifier> coveredComponents,
        Parameters wireParameters,
        DateTimeOffset? created,
        DateTimeOffset? expires,
        string? nonce,
        string? algorithm,
        string? keyId,
        string? tag)
    {
        CoveredComponents = Array.AsReadOnly(coveredComponents.ToArray());
        _wireParameters = new Parameters(wireParameters);
        Created = created;
        Expires = expires;
        Nonce = nonce;
        Algorithm = algorithm;
        KeyId = keyId;
        Tag = tag;
    }

    /// <summary>Gets the ordered list of covered component identifiers.</summary>
    public IReadOnlyList<ComponentIdentifier> CoveredComponents { get; }

    /// <summary>Gets the Unix timestamp of when the signature was created.</summary>
    public DateTimeOffset? Created { get; init; }

    /// <summary>Gets the Unix timestamp of when the signature expires.</summary>
    public DateTimeOffset? Expires { get; init; }

    /// <summary>Gets the nonce value to prevent signature replay attacks.</summary>
    public string? Nonce { get; init; }

    /// <summary>Gets the algorithm name as registered in the HTTP Signature Algorithms registry.</summary>
    public string? Algorithm { get; init; }

    /// <summary>Gets the key identifier used to select the signing/verification key.</summary>
    public string? KeyId { get; init; }

    /// <summary>Gets the application-specific tag value.</summary>
    public string? Tag { get; init; }

    /// <summary>
    /// Serializes these signature parameters to the Inner List format used in
    /// the signature base and <c>Signature-Input</c> header.
    /// Example: <c>("@method" "@authority" "content-type");created=1618884473;keyid="test-key"</c>
    /// </summary>
    /// <returns>The serialized Inner List string.</returns>
    public string Serialize()
    {
        if (_wireParameters is not null)
        {
            // Full-fidelity round trip: reproduce the exact wire metadata parameter order,
            // including any parameters not represented by a typed property.
            var wireList = new InnerList(CoveredComponents.Select(c => c.ToStructuredFieldItem()), _wireParameters);
            return StructuredFieldSerializer.SerializeInnerList(wireList);
        }

        var innerList = new InnerList(CoveredComponents.Select(c => c.ToStructuredFieldItem()));

        // Signature parameters — order per RFC 9421 Appendix B test vectors:
        // created, expires, keyid, nonce, alg, tag
        if (Created.HasValue)
            innerList.Parameters.Add("created", new IntegerItem(Created.Value.ToUnixTimeSeconds()));
        if (Expires.HasValue)
            innerList.Parameters.Add("expires", new IntegerItem(Expires.Value.ToUnixTimeSeconds()));
        if (KeyId is not null)
            innerList.Parameters.Add("keyid", new StringItem(KeyId));
        if (Nonce is not null)
            innerList.Parameters.Add("nonce", new StringItem(Nonce));
        if (Algorithm is not null)
            innerList.Parameters.Add("alg", new StringItem(Algorithm));
        if (Tag is not null)
            innerList.Parameters.Add("tag", new StringItem(Tag));

        return StructuredFieldSerializer.SerializeInnerList(innerList);
    }

    /// <summary>
    /// Parses signature parameters from a Signature-Input dictionary member (an <see cref="InnerList"/> with parameters).
    /// </summary>
    /// <param name="innerList">The inner list representing the signature parameters.</param>
    /// <returns>The parsed <see cref="SignatureParameters"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when innerList is null.</exception>
    /// <exception cref="FormatException">Thrown when the inner list cannot be parsed as signature parameters.</exception>
    public static SignatureParameters Parse(InnerList innerList)
    {
        ArgumentNullException.ThrowIfNull(innerList);

        var coveredComponents = new List<ComponentIdentifier>(innerList.Count);

        for (var i = 0; i < innerList.Count; i++)
        {
            var item = innerList[i];
            if (item.Value is not StringItem nameItem)
            {
                throw new FormatException(
                    $"Component identifier at index {i} must be an SF String, but was {item.Value.GetType().Name}.");
            }

            // Preserves unknown component parameters and validates reserved parameter types;
            // does not silently repair a non-lowercase wire name.
            coveredComponents.Add(ComponentIdentifier.FromWire(nameItem.StringValue, item.Parameters));
        }

        var sigParams = innerList.Parameters;

        DateTimeOffset? created = null;
        if (sigParams.TryGetValue("created", out var createdItem))
        {
            if (createdItem is not IntegerItem createdInt)
                throw new FormatException("Signature parameter 'created' must be an SF Integer.");
            created = ParseTimestamp("created", createdInt.LongValue);
        }

        DateTimeOffset? expires = null;
        if (sigParams.TryGetValue("expires", out var expiresItem))
        {
            if (expiresItem is not IntegerItem expiresInt)
                throw new FormatException("Signature parameter 'expires' must be an SF Integer.");
            expires = ParseTimestamp("expires", expiresInt.LongValue);
        }

        string? nonce = null;
        if (sigParams.TryGetValue("nonce", out var nonceItem))
        {
            if (nonceItem is not StringItem nonceStr)
                throw new FormatException("Signature parameter 'nonce' must be an SF String.");
            nonce = nonceStr.StringValue;
        }

        string? alg = null;
        if (sigParams.TryGetValue("alg", out var algItem))
        {
            if (algItem is not StringItem algStr)
                throw new FormatException("Signature parameter 'alg' must be an SF String.");
            alg = algStr.StringValue;
        }

        string? keyId = null;
        if (sigParams.TryGetValue("keyid", out var keyIdItem))
        {
            if (keyIdItem is not StringItem keyIdStr)
                throw new FormatException("Signature parameter 'keyid' must be an SF String.");
            keyId = keyIdStr.StringValue;
        }

        string? tag = null;
        if (sigParams.TryGetValue("tag", out var tagItem))
        {
            if (tagItem is not StringItem tagStr)
                throw new FormatException("Signature parameter 'tag' must be an SF String.");
            tag = tagStr.StringValue;
        }

        return new SignatureParameters(coveredComponents, sigParams, created, expires, nonce, alg, keyId, tag);
    }

    private static DateTimeOffset ParseTimestamp(string parameterName, long value)
    {
        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(value);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new FormatException(
                $"Signature parameter '{parameterName}' is outside the supported timestamp range.",
                ex);
        }
    }
}
