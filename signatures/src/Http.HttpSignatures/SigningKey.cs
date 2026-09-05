// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Abstract base class for key material used in signing operations.
/// Concrete implementations wrap specific .NET cryptographic key types.
/// </summary>
public abstract class SigningKey
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SigningKey"/> class.
    /// </summary>
    /// <param name="keyId">The key identifier used in the <c>keyid</c> signature parameter.</param>
    protected SigningKey(string keyId)
    {
        ArgumentException.ThrowIfNullOrEmpty(keyId);
        KeyId = keyId;
    }

    /// <summary>
    /// Gets the key identifier used in the <c>keyid</c> signature parameter.
    /// </summary>
    public string KeyId { get; }

}
