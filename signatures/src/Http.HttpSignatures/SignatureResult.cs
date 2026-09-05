// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// The result of a signing operation, containing the values to add to
/// <c>Signature-Input</c> and <c>Signature</c> HTTP headers.
/// </summary>
public sealed class SignatureResult
{
    private readonly byte[] _signatureBytes;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignatureResult"/> class.
    /// </summary>
    /// <param name="label">The signature label.</param>
    /// <param name="signatureInputHeaderValue">The serialized <c>Signature-Input</c> member value.</param>
    /// <param name="signatureHeaderValue">The serialized <c>Signature</c> member value.</param>
    /// <param name="signatureBytes">The raw signature bytes. Cloned defensively; the caller's array is not retained.</param>
    public SignatureResult(
        string label,
        string signatureInputHeaderValue,
        string signatureHeaderValue,
        byte[] signatureBytes)
    {
        ArgumentNullException.ThrowIfNull(signatureBytes);

        Label = label;
        SignatureInputHeaderValue = signatureInputHeaderValue;
        SignatureHeaderValue = signatureHeaderValue;
        _signatureBytes = (byte[])signatureBytes.Clone();
    }

    /// <summary>Gets the signature label (e.g., "sig1").</summary>
    public string Label { get; }

    /// <summary>
    /// Gets the value to add/append to the <c>Signature-Input</c> header.
    /// Format: <c>label=("@method" "@authority");created=1618884473;keyid="test"</c>
    /// </summary>
    public string SignatureInputHeaderValue { get; }

    /// <summary>
    /// Gets the value to add/append to the <c>Signature</c> header.
    /// Format: <c>label=:base64signature:</c>
    /// </summary>
    public string SignatureHeaderValue { get; }

    /// <summary>
    /// Gets read-only access to the immutable raw signature bytes, without exposing the backing array.
    /// </summary>
    public ReadOnlySpan<byte> SignatureBytes => _signatureBytes;

    /// <summary>Returns a new defensive copy of the raw signature bytes.</summary>
    public byte[] ToArray() => (byte[])_signatureBytes.Clone();
}
