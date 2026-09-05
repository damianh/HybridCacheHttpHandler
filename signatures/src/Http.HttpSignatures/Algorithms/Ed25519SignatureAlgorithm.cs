// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.HttpSignatures.Algorithms;

using DamianH.Http.HttpSignatures.Keys;

/// <summary>
/// Ed25519 signature algorithm per RFC 9421 §3.3.6.
/// .NET does not currently provide built-in Ed25519 support.
/// This implementation throws <see cref="PlatformNotSupportedException"/> at runtime.
/// When .NET adds <c>System.Security.Cryptography.EdDSA</c> support,
/// this implementation can be updated to use it.
/// </summary>
public sealed class Ed25519SignatureAlgorithm : ISignatureAlgorithm
{
    /// <inheritdoc/>
    public string AlgorithmName => "ed25519";

    /// <inheritdoc/>
    public bool IsCompatible(SigningKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key is Ed25519SigningKey;
    }

    /// <inheritdoc/>
    public bool IsCompatible(VerificationKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key is Ed25519VerificationKey;
    }

    /// <inheritdoc/>
    public byte[] Sign(ReadOnlySpan<byte> signatureBase, SigningKey key) =>
        throw new PlatformNotSupportedException(
            "Ed25519 is not supported by the current .NET runtime. " +
            "A future .NET version or a third-party library is required for Ed25519 support.");

    /// <inheritdoc/>
    public bool Verify(ReadOnlySpan<byte> signatureBase, VerificationKey key, ReadOnlySpan<byte> signature) =>
        throw new PlatformNotSupportedException(
            "Ed25519 is not supported by the current .NET runtime. " +
            "A future .NET version or a third-party library is required for Ed25519 support.");
}
