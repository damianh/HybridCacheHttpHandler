// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.HttpSignatures.Keys;

/// <summary>
/// Shared secret key material for HMAC-based signature algorithms.
/// Used as both <see cref="SigningKey"/> and <see cref="VerificationKey"/>
/// since HMAC is a symmetric algorithm.
/// </summary>
public sealed class HmacSharedKey : SigningKey
{
    private readonly byte[] _keyBytes;

    /// <summary>
    /// Initializes a new instance of the <see cref="HmacSharedKey"/> class.
    /// </summary>
    /// <param name="keyId">The key identifier.</param>
    /// <param name="keyBytes">The shared secret key bytes.</param>
    public HmacSharedKey(string keyId, byte[] keyBytes)
        : base(keyId)
    {
        ArgumentNullException.ThrowIfNull(keyBytes);
        _keyBytes = (byte[])keyBytes.Clone();
    }

    /// <summary>Gets read-only access to the shared secret key bytes.</summary>
    public ReadOnlySpan<byte> KeyBytes => _keyBytes;

    /// <summary>
    /// Gets this key as a <see cref="VerificationKey"/>.
    /// </summary>
    public HmacSharedVerificationKey AsVerificationKey() => new(KeyId, _keyBytes);
}

/// <summary>
/// Verification-side wrapper for HMAC shared key material.
/// </summary>
public sealed class HmacSharedVerificationKey : VerificationKey
{
    private readonly byte[] _keyBytes;

    /// <summary>
    /// Initializes a new instance of the <see cref="HmacSharedVerificationKey"/> class.
    /// </summary>
    /// <param name="keyId">The key identifier.</param>
    /// <param name="keyBytes">The shared secret key bytes.</param>
    public HmacSharedVerificationKey(string keyId, byte[] keyBytes)
        : base(keyId)
    {
        ArgumentNullException.ThrowIfNull(keyBytes);
        _keyBytes = (byte[])keyBytes.Clone();
    }

    /// <summary>Gets read-only access to the shared secret key bytes.</summary>
    public ReadOnlySpan<byte> KeyBytes => _keyBytes;
}
