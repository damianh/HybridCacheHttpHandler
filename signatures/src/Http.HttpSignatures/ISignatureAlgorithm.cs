// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Represents an HTTP message signature algorithm per RFC 9421 §3.3.
/// Implementations provide the cryptographic sign and verify primitives.
/// </summary>
public interface ISignatureAlgorithm
{
    /// <summary>
    /// Gets the algorithm identifier as registered in the HTTP Signature Algorithms registry
    /// (e.g., "rsa-pss-sha512", "hmac-sha256").
    /// </summary>
    string AlgorithmName { get; }

    /// <summary>
    /// Determines whether this algorithm can use the supplied signing key.
    /// </summary>
    /// <param name="key">The signing key to inspect.</param>
    /// <returns><see langword="true"/> when the key is compatible.</returns>
    bool IsCompatible(SigningKey key);

    /// <summary>
    /// Determines whether this algorithm can use the supplied verification key.
    /// </summary>
    /// <param name="key">The verification key to inspect.</param>
    /// <returns><see langword="true"/> when the key is compatible.</returns>
    bool IsCompatible(VerificationKey key);

    /// <summary>
    /// Signs the signature base, producing the signature bytes.
    /// Implements the <c>HTTP_SIGN(M, Ks) → S</c> primitive from RFC 9421 §3.3.
    /// </summary>
    /// <param name="signatureBase">The signature base bytes (ASCII).</param>
    /// <param name="key">The signing key material.</param>
    /// <returns>The raw signature bytes.</returns>
    byte[] Sign(ReadOnlySpan<byte> signatureBase, SigningKey key);

    /// <summary>
    /// Verifies the signature against the signature base.
    /// Implements the <c>HTTP_VERIFY(M, Kv, S) → bool</c> primitive from RFC 9421 §3.3.
    /// </summary>
    /// <param name="signatureBase">The signature base bytes (ASCII).</param>
    /// <param name="key">The verification key material.</param>
    /// <param name="signature">The signature bytes to verify.</param>
    /// <returns><see langword="true"/> if the signature is valid; otherwise, <see langword="false"/>.</returns>
    bool Verify(ReadOnlySpan<byte> signatureBase, VerificationKey key, ReadOnlySpan<byte> signature);
}
