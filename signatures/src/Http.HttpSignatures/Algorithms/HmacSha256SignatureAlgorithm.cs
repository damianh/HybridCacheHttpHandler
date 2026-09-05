// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using DamianH.Http.HttpSignatures.Keys;

namespace DamianH.Http.HttpSignatures.Algorithms;

/// <summary>
/// HMAC-SHA256 signature algorithm per RFC 9421 §3.3.3.
/// Uses <see cref="HMACSHA256"/> with a shared secret key.
/// This is a symmetric (deterministic) algorithm.
/// </summary>
public sealed class HmacSha256SignatureAlgorithm : ISignatureAlgorithm
{
    /// <inheritdoc/>
    public string AlgorithmName => "hmac-sha256";

    /// <inheritdoc/>
    public bool IsCompatible(SigningKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key is HmacSharedKey;
    }

    /// <inheritdoc/>
    public bool IsCompatible(VerificationKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key is HmacSharedVerificationKey;
    }

    /// <inheritdoc/>
    public byte[] Sign(ReadOnlySpan<byte> signatureBase, SigningKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (key is not HmacSharedKey hmacKey)
            throw new ArgumentException(
                $"Expected {nameof(HmacSharedKey)} but received {key.GetType().Name}.", nameof(key));

        return HMACSHA256.HashData(hmacKey.KeyBytes, signatureBase);
    }

    /// <inheritdoc/>
    public bool Verify(ReadOnlySpan<byte> signatureBase, VerificationKey key, ReadOnlySpan<byte> signature)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (key is not HmacSharedVerificationKey hmacKey)
            throw new ArgumentException(
                $"Expected {nameof(HmacSharedVerificationKey)} but received {key.GetType().Name}.", nameof(key));

        var expected = HMACSHA256.HashData(hmacKey.KeyBytes, signatureBase);
        return CryptographicOperations.FixedTimeEquals(expected, signature);
    }
}
