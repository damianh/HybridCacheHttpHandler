// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;

namespace DamianH.Http.HttpSignatures.Keys;

/// <summary>
/// RSA signing key material wrapping an <see cref="System.Security.Cryptography.RSA"/> instance.
/// </summary>
public sealed class RsaSigningKey : SigningKey
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RsaSigningKey"/> class.
    /// </summary>
    /// <param name="keyId">The key identifier.</param>
    /// <param name="rsa">The RSA key (must contain the private key).</param>
    public RsaSigningKey(string keyId, RSA rsa)
        : base(keyId)
    {
        ArgumentNullException.ThrowIfNull(rsa);
        Rsa = rsa;
    }

    /// <summary>Gets the RSA key.</summary>
    public RSA Rsa { get; }

}

/// <summary>
/// RSA verification key material wrapping an <see cref="System.Security.Cryptography.RSA"/> instance.
/// </summary>
public sealed class RsaVerificationKey : VerificationKey
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RsaVerificationKey"/> class.
    /// </summary>
    /// <param name="keyId">The key identifier.</param>
    /// <param name="rsa">The RSA key (public key only is sufficient).</param>
    public RsaVerificationKey(string keyId, RSA rsa)
        : base(keyId)
    {
        ArgumentNullException.ThrowIfNull(rsa);
        Rsa = rsa;
    }

    /// <summary>Gets the RSA key.</summary>
    public RSA Rsa { get; }

}
