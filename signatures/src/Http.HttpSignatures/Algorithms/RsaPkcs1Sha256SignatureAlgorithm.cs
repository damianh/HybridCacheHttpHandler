// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using DamianH.Http.HttpSignatures.Keys;

namespace DamianH.Http.HttpSignatures.Algorithms;

/// <summary>
/// RSA PKCS#1 v1.5 SHA-256 signature algorithm per RFC 9421 §3.3.2.
/// Uses <see cref="RSA"/> with <see cref="RSASignaturePadding.Pkcs1"/> and <see cref="HashAlgorithmName.SHA256"/>.
/// </summary>
public sealed class RsaPkcs1Sha256SignatureAlgorithm : ISignatureAlgorithm
{
    /// <inheritdoc/>
    public string AlgorithmName => "rsa-v1_5-sha256";

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
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
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
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
    }
}
