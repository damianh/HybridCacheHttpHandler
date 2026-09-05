// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using DamianH.Http.HttpSignatures.Keys;

namespace DamianH.Http.HttpSignatures.Algorithms;

/// <summary>
/// ECDSA P-256 SHA-256 signature algorithm per RFC 9421 §3.3.4.
/// Uses <see cref="ECDsa"/> with the P-256 curve and SHA-256.
/// Signature format is IEEE P1363 (<c>r || s</c> concatenation, 64 bytes total).
/// This is a non-deterministic algorithm.
/// </summary>
public sealed class EcdsaP256Sha256SignatureAlgorithm : ISignatureAlgorithm
{
    private const string P256Oid = "1.2.840.10045.3.1.7";

    /// <inheritdoc/>
    public string AlgorithmName => "ecdsa-p256-sha256";

    /// <inheritdoc/>
    public bool IsCompatible(SigningKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return EcdsaKeyCompatibility.IsSigningKey(key, P256Oid);
    }

    /// <inheritdoc/>
    public bool IsCompatible(VerificationKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return EcdsaKeyCompatibility.IsVerificationKey(key, P256Oid);
    }

    /// <inheritdoc/>
    public byte[] Sign(ReadOnlySpan<byte> signatureBase, SigningKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (!IsCompatible(key) || key is not EcdsaSigningKey ecdsaKey)
            throw new ArgumentException(
                $"Expected a P-256 {nameof(EcdsaSigningKey)} but received {key.GetType().Name}.", nameof(key));

        return ecdsaKey.Ecdsa.SignData(
            signatureBase,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    /// <inheritdoc/>
    public bool Verify(ReadOnlySpan<byte> signatureBase, VerificationKey key, ReadOnlySpan<byte> signature)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (!IsCompatible(key) || key is not EcdsaVerificationKey ecdsaKey)
            throw new ArgumentException(
                $"Expected a P-256 {nameof(EcdsaVerificationKey)} but received {key.GetType().Name}.", nameof(key));

        if (signature.Length != 64)
            return false;

        return ecdsaKey.Ecdsa.VerifyData(
            signatureBase,
            signature,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }
}
