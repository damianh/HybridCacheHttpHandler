namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Atomically records nonce use for replay protection across all application instances that share
/// the same scope.
/// </summary>
public interface INonceStore
{
    /// <summary>
    /// Attempts to exclusively claim a previously unused nonce until the supplied deadline.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> only when this operation atomically recorded a nonce that had not
    /// already been claimed; <see langword="false"/> when the nonce is a replay.
    /// </returns>
    ValueTask<bool> TryUseAsync(
        string scope,
        string credentialKeyId,
        string nonce,
        DateTimeOffset retainUntil,
        CancellationToken cancellationToken = default);
}
