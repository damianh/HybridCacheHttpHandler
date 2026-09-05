namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Resolves trusted verification credentials from a signed key identifier.
/// </summary>
public interface IVerificationCredentialsResolver
{
    /// <summary>Resolves credentials trusted for the supplied key identifier.</summary>
    ValueTask<VerificationCredentials?> ResolveAsync(
        string keyId,
        CancellationToken cancellationToken = default);
}
