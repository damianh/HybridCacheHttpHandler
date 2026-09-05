namespace DamianH.Http.HttpSignatures;

/// <summary>Identifies why a cryptographically verified signature was not accepted by policy.</summary>
public enum VerificationAcceptanceFailureCode
{
    /// <summary>No acceptance failure occurred.</summary>
    None,
    /// <summary>Protocol or cryptographic verification failed.</summary>
    VerificationFailed,
    /// <summary>A required covered component is absent.</summary>
    MissingRequiredComponent,
    /// <summary>A required creation time is absent.</summary>
    MissingCreated,
    /// <summary>The creation time is later than the allowed clock skew.</summary>
    CreatedInFuture,
    /// <summary>The signature is older than the configured maximum age.</summary>
    SignatureTooOld,
    /// <summary>A required expiration time is absent.</summary>
    MissingExpires,
    /// <summary>The signature has passed its accepted expiration time.</summary>
    SignatureExpired,
    /// <summary>The required application tag is absent or different.</summary>
    TagMismatch,
    /// <summary>A required nonce is absent.</summary>
    MissingNonce,
    /// <summary>The nonce was already claimed.</summary>
    NonceReplayed,
}
