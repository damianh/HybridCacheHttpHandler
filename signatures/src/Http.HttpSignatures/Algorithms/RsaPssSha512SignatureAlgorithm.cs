// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using DamianH.Http.HttpSignatures.Keys;

namespace DamianH.Http.HttpSignatures.Algorithms;

/// <summary>
/// RSA-PSS-SHA512 signature algorithm per RFC 9421 §3.3.1.
/// Uses <see cref="RSA"/> with <see cref="RSASignaturePadding.Pss"/> and <see cref="HashAlgorithmName.SHA512"/>.
/// The salt length is equal to the hash output length (64 bytes) per the RFC requirement.
/// This is a non-deterministic algorithm.
/// </summary>
public sealed class RsaPssSha512SignatureAlgorithm : ISignatureAlgorithm
{
    /// <inheritdoc/>
    public string AlgorithmName => "rsa-pss-sha512";

    /// <inheritdoc/>
    public bool IsCompatible(SigningKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key is RsaSigningKey;
    }

    /// <inheritdoc/>
    public bool IsCompatible(VerificationKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key is RsaVerificationKey;
    }

    /// <inheritdoc/>
    public byte[] Sign(ReadOnlySpan<byte> signatureBase, SigningKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (key is not RsaSigningKey rsaKey)
            throw new ArgumentException(
                $"Expected {nameof(RsaSigningKey)} but received {key.GetType().Name}.", nameof(key));

        return rsaKey.Rsa.SignData(
            signatureBase,
            HashAlgorithmName.SHA512,
            RSASignaturePadding.Pss);
    }

    /// <inheritdoc/>
    public bool Verify(ReadOnlySpan<byte> signatureBase, VerificationKey key, ReadOnlySpan<byte> signature)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (key is not RsaVerificationKey rsaKey)
            throw new ArgumentException(
                $"Expected {nameof(RsaVerificationKey)} but received {key.GetType().Name}.", nameof(key));

        return rsaKey.Rsa.VerifyData(
            signatureBase,
            signature,
            HashAlgorithmName.SHA512,
            RSASignaturePadding.Pss);
    }
}
