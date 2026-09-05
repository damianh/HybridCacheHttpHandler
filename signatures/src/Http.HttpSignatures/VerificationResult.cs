// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// The result of a signature verification operation.
/// </summary>
public sealed class VerificationResult
{
    private VerificationResult(
        bool isValid,
        SignatureParameters? parameters,
        string? credentialKeyId,
        VerificationFailureCode failureCode,
        string? errorMessage)
    {
        IsValid = isValid;
        Parameters = parameters;
        CredentialKeyId = credentialKeyId;
        FailureCode = failureCode;
        ErrorMessage = errorMessage;
    }

    /// <summary>Gets a value indicating whether the signature is valid.</summary>
    public bool IsValid { get; }

    /// <summary>Gets the parsed signature parameters if available.</summary>
    public SignatureParameters? Parameters { get; }

    /// <summary>Gets the trusted credential identity used for successful verification.</summary>
    public string? CredentialKeyId { get; }

    /// <summary>Gets the machine-readable failure code.</summary>
    public VerificationFailureCode FailureCode { get; }

    /// <summary>Gets the error message if verification failed.</summary>
    public string? ErrorMessage { get; }

    /// <summary>Creates a successful verification result.</summary>
    /// <param name="parameters">The verified signature parameters.</param>
    /// <param name="credentialKeyId">The trusted credential identity.</param>
    internal static VerificationResult Success(SignatureParameters parameters, string credentialKeyId) =>
        new(true, parameters, credentialKeyId, VerificationFailureCode.None, null);

    /// <summary>Creates a failed verification result.</summary>
    /// <param name="failureCode">The machine-readable failure code.</param>
    /// <param name="errorMessage">Description of why verification failed.</param>
    /// <param name="parameters">The parsed signature parameters, if available.</param>
    internal static VerificationResult Failure(
        VerificationFailureCode failureCode,
        string errorMessage,
        SignatureParameters? parameters = null) =>
        new(false, parameters, null, failureCode, errorMessage);
}
