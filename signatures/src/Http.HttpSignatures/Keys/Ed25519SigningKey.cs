// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.HttpSignatures.Keys;

/// <summary>
/// Ed25519 signing key material.
/// Wraps raw Ed25519 private key bytes (32 bytes).
/// </summary>
public sealed class Ed25519SigningKey : SigningKey
{
    private readonly byte[] _privateKeyBytes;

    /// <summary>
    /// Initializes a new instance of the <see cref="Ed25519SigningKey"/> class.
    /// </summary>
    /// <param name="keyId">The key identifier.</param>
    /// <param name="privateKeyBytes">The raw Ed25519 private key bytes (32 bytes).</param>
    public Ed25519SigningKey(string keyId, byte[] privateKeyBytes)
        : base(keyId)
    {
        ArgumentNullException.ThrowIfNull(privateKeyBytes);
        _privateKeyBytes = (byte[])privateKeyBytes.Clone();
    }

    /// <summary>Gets read-only access to the raw Ed25519 private key bytes.</summary>
    public ReadOnlySpan<byte> PrivateKeyBytes => _privateKeyBytes;

}

/// <summary>
/// Ed25519 verification key material.
/// Wraps raw Ed25519 public key bytes (32 bytes).
/// </summary>
public sealed class Ed25519VerificationKey : VerificationKey
{
    private readonly byte[] _publicKeyBytes;

    /// <summary>
    /// Initializes a new instance of the <see cref="Ed25519VerificationKey"/> class.
    /// </summary>
    /// <param name="keyId">The key identifier.</param>
    /// <param name="publicKeyBytes">The raw Ed25519 public key bytes (32 bytes).</param>
    public Ed25519VerificationKey(string keyId, byte[] publicKeyBytes)
        : base(keyId)
    {
        ArgumentNullException.ThrowIfNull(publicKeyBytes);
        _publicKeyBytes = (byte[])publicKeyBytes.Clone();
    }

    /// <summary>Gets read-only access to the raw Ed25519 public key bytes.</summary>
    public ReadOnlySpan<byte> PublicKeyBytes => _publicKeyBytes;

}
