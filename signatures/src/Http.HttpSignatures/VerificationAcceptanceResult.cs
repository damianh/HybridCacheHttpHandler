namespace DamianH.Http.HttpSignatures;

/// <summary>Combines cryptographic verification with explicit application policy acceptance.</summary>
public sealed class VerificationAcceptanceResult
{
    private VerificationAcceptanceResult(
        VerificationResult verification,
        bool isAccepted,
        VerificationAcceptanceFailureCode failureCode,
        string? errorMessage)
    {
        Verification = verification;
        IsAccepted = isAccepted;
        FailureCode = failureCode;
        ErrorMessage = errorMessage;
    }

    /// <summary>Gets the underlying protocol and cryptographic verification result.</summary>
    public VerificationResult Verification { get; }

    /// <summary>Gets whether the verified signature satisfies the explicit application policy.</summary>
    public bool IsAccepted { get; }

    /// <summary>Gets the machine-readable acceptance failure.</summary>
    public VerificationAcceptanceFailureCode FailureCode { get; }

    /// <summary>Gets the human-readable acceptance failure.</summary>
    public string? ErrorMessage { get; }

    internal static VerificationAcceptanceResult Accepted(VerificationResult verification) =>
        new(verification, true, VerificationAcceptanceFailureCode.None, null);

    internal static VerificationAcceptanceResult Rejected(
        VerificationResult verification,
        VerificationAcceptanceFailureCode failureCode,
        string errorMessage) =>
        new(verification, false, failureCode, errorMessage);
}
