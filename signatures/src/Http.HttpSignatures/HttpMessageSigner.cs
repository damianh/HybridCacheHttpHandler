// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Creates HTTP message signatures per RFC 9421 §3.1.
/// Constructs the signature base, signs it, and produces header values.
/// </summary>
public sealed class HttpMessageSigner
{
    /// <summary>
    /// Signs an HTTP message, producing <c>Signature-Input</c> and <c>Signature</c> header values.
    /// </summary>
    /// <param name="label">The signature label (e.g., "sig1").</param>
    /// <param name="context">The HTTP message context to sign.</param>
    /// <param name="parameters">The signature parameters defining covered components and metadata.</param>
    /// <param name="credentials">The trusted signing key and algorithm.</param>
    /// <param name="fieldTypeResolver">
    /// Declares the Structured Field type of HTTP fields, used to resolve <c>sf</c> and <c>key</c>
    /// components. When null, every field's type is treated as unknown, so <c>sf</c>/<c>key</c>
    /// components fail explicitly instead of guessing the type.
    /// </param>
    /// <returns>A <see cref="SignatureResult"/> containing the header values.</returns>
    public SignatureResult Sign(
        string label,
        IHttpMessageContext context,
        SignatureParameters parameters,
        SigningCredentials credentials,
        IStructuredFieldTypeResolver? fieldTypeResolver = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(label);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(credentials);

        if (parameters.KeyId is not null &&
            !string.Equals(parameters.KeyId, credentials.Key.KeyId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Signature keyid '{parameters.KeyId}' does not match credential key '{credentials.Key.KeyId}'.",
                nameof(parameters));
        }

        if (parameters.Algorithm is not null &&
            !string.Equals(parameters.Algorithm, credentials.Algorithm.AlgorithmName, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Signature algorithm '{parameters.Algorithm}' does not match credential algorithm " +
                $"'{credentials.Algorithm.AlgorithmName}'.",
                nameof(parameters));
        }

        var signatureBase = SignatureBaseBuilder.Build(parameters, context, fieldTypeResolver);
        var signatureBytes = credentials.Algorithm.Sign(signatureBase, credentials.Key);
        var signatureInputHeaderValue = SignatureHeaderParser.SerializeSignatureInput(label, parameters);
        var signatureHeaderValue = SignatureHeaderParser.SerializeSignature(label, signatureBytes);

        return new SignatureResult(
            label,
            signatureInputHeaderValue,
            signatureHeaderValue,
            signatureBytes);
    }

    /// <summary>
    /// Signs an HTTP message using key material and an algorithm that are validated as credentials.
    /// </summary>
    public SignatureResult Sign(
        string label,
        IHttpMessageContext context,
        SignatureParameters parameters,
        SigningKey key,
        ISignatureAlgorithm algorithm,
        IStructuredFieldTypeResolver? fieldTypeResolver = null) =>
        Sign(label, context, parameters, new SigningCredentials(key, algorithm), fieldTypeResolver);
}
