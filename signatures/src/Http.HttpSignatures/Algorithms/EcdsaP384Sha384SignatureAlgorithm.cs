// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using DamianH.Http.HttpSignatures.Keys;

namespace DamianH.Http.HttpSignatures.Algorithms;

/// <summary>
/// ECDSA P-384 SHA-384 signature algorithm per RFC 9421 §3.3.5.
/// Uses <see cref="ECDsa"/> with the P-384 curve and SHA-384.
/// Signature format is IEEE P1363 (<c>r || s</c> concatenation, 96 bytes total).
/// This is a non-deterministic algorithm.
/// </summary>
public sealed class EcdsaP384Sha384SignatureAlgorithm : ISignatureAlgorithm
{
    private const string P384Oid = "1.3.132.0.34";

    /// <inheritdoc/>
    public string AlgorithmName => "ecdsa-p384-sha384";

    /// <inheritdoc/>
    public bool IsCompatible(SigningKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return EcdsaKeyCompatibility.IsSigningKey(key, P384Oid);
    }

    /// <inheritdoc/>
    public bool IsCompatible(VerificationKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return EcdsaKeyCompatibility.IsVerificationKey(key, P384Oid);
    }

    /// <inheritdoc/>
    public byte[] Sign(ReadOnlySpan<byte> signatureBase, SigningKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (!IsCompatible(key) || key is not EcdsaSigningKey ecdsaKey)
            throw new ArgumentException(
                $"Expected a P-384 {nameof(EcdsaSigningKey)} but received {key.GetType().Name}.", nameof(key));

        return ecdsaKey.Ecdsa.SignData(
            signatureBase,
            HashAlgorithmName.SHA384,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    /// <inheritdoc/>
    public bool Verify(ReadOnlySpan<byte> signatureBase, VerificationKey key, ReadOnlySpan<byte> signature)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (!IsCompatible(key) || key is not EcdsaVerificationKey ecdsaKey)
            throw new ArgumentException(
                $"Expected a P-384 {nameof(EcdsaVerificationKey)} but received {key.GetType().Name}.", nameof(key));

        if (signature.Length != 96)
            return false;

        return ecdsaKey.Ecdsa.VerifyData(
            signatureBase,
            signature,
            HashAlgorithmName.SHA384,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }
}
