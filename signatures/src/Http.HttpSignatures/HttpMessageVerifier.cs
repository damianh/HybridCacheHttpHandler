namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Performs protocol and cryptographic verification of HTTP message signatures per RFC 9421 §3.2.
/// Application acceptance requirements are applied separately by the policy-aware APIs.
/// </summary>
public sealed class HttpMessageVerifier
{
    /// <summary>Verifies a labeled signature with trusted credentials.</summary>
    public VerificationResult Verify(
        string label,
        IHttpMessageContext context,
        VerificationCredentials credentials,
        IStructuredFieldTypeResolver? fieldTypeResolver = null)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        var prepared = Prepare(label, context, fieldTypeResolver);
        return prepared.Failure ??
            VerifyPrepared(prepared.Signature!, credentials);
    }

    /// <summary>
    /// Verifies a labeled signature using key material and an algorithm that are first bound as
    /// trusted credentials.
    /// </summary>
    public VerificationResult Verify(
        string label,
        IHttpMessageContext context,
        VerificationKey key,
        ISignatureAlgorithm algorithm,
        IStructuredFieldTypeResolver? fieldTypeResolver = null) =>
        Verify(label, context, new VerificationCredentials(key, algorithm), fieldTypeResolver);

    /// <summary>
    /// Verifies a labeled signature using trusted credentials resolved from its signed key identifier.
    /// The message and signature base are parsed and snapshotted before awaiting the resolver.
    /// </summary>
    public async ValueTask<VerificationResult> VerifyAsync(
        string label,
        IHttpMessageContext context,
        IVerificationCredentialsResolver credentialsResolver,
        IStructuredFieldTypeResolver? fieldTypeResolver = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentialsResolver);

        var prepared = Prepare(label, context, fieldTypeResolver);
        if (prepared.Failure is not null)
            return prepared.Failure;

        var signature = prepared.Signature!;
        var keyId = signature.Parameters.KeyId;
        if (keyId is null)
        {
            return VerificationResult.Failure(
                VerificationFailureCode.MissingKeyId,
                "Signature parameters do not specify a keyid.",
                signature.Parameters);
        }

        var credentials = await credentialsResolver.ResolveAsync(keyId, cancellationToken);
        if (credentials is null)
        {
            return VerificationResult.Failure(
                VerificationFailureCode.CredentialsNotFound,
                $"Credentials for key '{keyId}' could not be resolved.",
                signature.Parameters);
        }

        return VerifyPrepared(signature, credentials);
    }

    /// <summary>
    /// Verifies a signature and applies explicit application acceptance requirements.
    /// </summary>
    public ValueTask<VerificationAcceptanceResult> VerifyAndValidateAsync(
        string label,
        IHttpMessageContext context,
        VerificationCredentials credentials,
        VerificationPolicy policy,
        IStructuredFieldTypeResolver? fieldTypeResolver = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var verification = Verify(label, context, credentials, fieldTypeResolver);
        return VerificationPolicyEvaluator.EvaluateAsync(verification, policy, cancellationToken);
    }

    /// <summary>
    /// Resolves trusted credentials, verifies a signature, and applies explicit application
    /// acceptance requirements.
    /// </summary>
    public async ValueTask<VerificationAcceptanceResult> VerifyAndValidateAsync(
        string label,
        IHttpMessageContext context,
        IVerificationCredentialsResolver credentialsResolver,
        VerificationPolicy policy,
        IStructuredFieldTypeResolver? fieldTypeResolver = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var verification = await VerifyAsync(
            label,
            context,
            credentialsResolver,
            fieldTypeResolver,
            cancellationToken);
        return await VerificationPolicyEvaluator.EvaluateAsync(
            verification,
            policy,
            cancellationToken);
    }

    private static PreparedVerification Prepare(
        string label,
        IHttpMessageContext context,
        IStructuredFieldTypeResolver? fieldTypeResolver)
    {
        ArgumentException.ThrowIfNullOrEmpty(label);
        ArgumentNullException.ThrowIfNull(context);
        SignatureHeaderParser.ValidateLabel(label);

        string? signatureInputRaw;
        try
        {
            signatureInputRaw = context.GetHeaderValue("signature-input");
        }
        catch (FormatException ex)
        {
            return PreparedVerification.FromFailure(
                VerificationResult.Failure(
                    VerificationFailureCode.MalformedSignatureInput,
                    $"Signature-Input header is malformed: {ex.Message}"));
        }

        if (signatureInputRaw is null)
        {
            return PreparedVerification.FromFailure(
                VerificationResult.Failure(
                    VerificationFailureCode.MissingSignatureInput,
                    "Signature-Input header not found."));
        }

        SignatureParameters? parameters;
        try
        {
            parameters = SignatureHeaderParser.ParseSignatureInput(signatureInputRaw, label);
        }
        catch (FormatException ex)
        {
            return PreparedVerification.FromFailure(
                VerificationResult.Failure(
                    VerificationFailureCode.MalformedSignatureInput,
                    $"Signature-Input header is malformed: {ex.Message}"));
        }

        if (parameters is null)
        {
            return PreparedVerification.FromFailure(
                VerificationResult.Failure(
                    VerificationFailureCode.SignatureInputLabelNotFound,
                    $"Signature label '{label}' not found in Signature-Input header."));
        }

        string? signatureRaw;
        try
        {
            signatureRaw = context.GetHeaderValue("signature");
        }
        catch (FormatException ex)
        {
            return PreparedVerification.FromFailure(
                VerificationResult.Failure(
                    VerificationFailureCode.MalformedSignature,
                    $"Signature header is malformed: {ex.Message}",
                    parameters));
        }

        if (signatureRaw is null)
        {
            return PreparedVerification.FromFailure(
                VerificationResult.Failure(
                    VerificationFailureCode.MissingSignature,
                    "Signature header not found.",
                    parameters));
        }

        byte[]? signatureBytes;
        try
        {
            signatureBytes = SignatureHeaderParser.ParseSignature(signatureRaw, label);
        }
        catch (FormatException ex)
        {
            return PreparedVerification.FromFailure(
                VerificationResult.Failure(
                    VerificationFailureCode.MalformedSignature,
                    $"Signature header is malformed: {ex.Message}",
                    parameters));
        }

        if (signatureBytes is null)
        {
            return PreparedVerification.FromFailure(
                VerificationResult.Failure(
                    VerificationFailureCode.SignatureLabelNotFound,
                    $"Signature label '{label}' not found in Signature header.",
                    parameters));
        }

        byte[] signatureBase;
        try
        {
            signatureBase = SignatureBaseBuilder.Build(parameters, context, fieldTypeResolver);
        }
        catch (SignatureBaseException ex)
        {
            return PreparedVerification.FromFailure(
                VerificationResult.Failure(
                    VerificationFailureCode.SignatureBaseInvalid,
                    $"Failed to construct signature base: {ex.Message}",
                    parameters));
        }
        catch (FormatException ex)
        {
            return PreparedVerification.FromFailure(
                VerificationResult.Failure(
                    VerificationFailureCode.SignatureBaseInvalid,
                    $"Failed to construct signature base: {ex.Message}",
                    parameters));
        }

        return PreparedVerification.FromSignature(
            new PreparedSignature(parameters, signatureBase, signatureBytes));
    }

    private static VerificationResult VerifyPrepared(
        PreparedSignature signature,
        VerificationCredentials credentials)
    {
        var parameters = signature.Parameters;

        if (parameters.KeyId is not null &&
            !string.Equals(parameters.KeyId, credentials.Key.KeyId, StringComparison.Ordinal))
        {
            return VerificationResult.Failure(
                VerificationFailureCode.CredentialKeyMismatch,
                $"Signature keyid '{parameters.KeyId}' does not match credential key " +
                $"'{credentials.Key.KeyId}'.",
                parameters);
        }

        if (parameters.Algorithm is not null &&
            !string.Equals(
                parameters.Algorithm,
                credentials.Algorithm.AlgorithmName,
                StringComparison.Ordinal))
        {
            return VerificationResult.Failure(
                VerificationFailureCode.AlgorithmMismatch,
                $"Signature algorithm '{parameters.Algorithm}' does not match credential algorithm " +
                $"'{credentials.Algorithm.AlgorithmName}'.",
                parameters);
        }

        if (!credentials.Algorithm.Verify(
            signature.SignatureBase,
            credentials.Key,
            signature.SignatureBytes))
        {
            return VerificationResult.Failure(
                VerificationFailureCode.CryptographicFailure,
                "Signature verification failed: cryptographic verification returned false.",
                parameters);
        }

        return VerificationResult.Success(parameters, credentials.Key.KeyId);
    }

    private sealed record PreparedSignature(
        SignatureParameters Parameters,
        byte[] SignatureBase,
        byte[] SignatureBytes);

    private sealed record PreparedVerification(
        PreparedSignature? Signature,
        VerificationResult? Failure)
    {
        internal static PreparedVerification FromSignature(PreparedSignature signature) =>
            new(signature, null);

        internal static PreparedVerification FromFailure(VerificationResult failure) =>
            new(null, failure);
    }
}
