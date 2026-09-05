namespace DamianH.Http.HttpSignatures;

/// <summary>Identifies an expected HTTP message signature verification failure.</summary>
public enum VerificationFailureCode
{
    /// <summary>No failure occurred.</summary>
    None,
    /// <summary>The message has no Signature-Input field.</summary>
    MissingSignatureInput,
    /// <summary>The Signature-Input field is malformed.</summary>
    MalformedSignatureInput,
    /// <summary>The requested label is absent from Signature-Input.</summary>
    SignatureInputLabelNotFound,
    /// <summary>The message has no Signature field.</summary>
    MissingSignature,
    /// <summary>The Signature field is malformed.</summary>
    MalformedSignature,
    /// <summary>The requested label is absent from Signature.</summary>
    SignatureLabelNotFound,
    /// <summary>The covered components cannot produce a valid signature base.</summary>
    SignatureBaseInvalid,
    /// <summary>Runtime credential resolution requires a missing keyid.</summary>
    MissingKeyId,
    /// <summary>No trusted credentials were resolved.</summary>
    CredentialsNotFound,
    /// <summary>The trusted credential identity conflicts with signed metadata.</summary>
    CredentialKeyMismatch,
    /// <summary>The trusted credential algorithm conflicts with signed metadata.</summary>
    AlgorithmMismatch,
    /// <summary>The cryptographic verification primitive returned false.</summary>
    CryptographicFailure,
}
