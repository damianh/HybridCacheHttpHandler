namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Defines explicit application acceptance requirements applied after protocol and cryptographic
/// signature verification.
/// </summary>
public sealed class VerificationPolicy
{
    /// <summary>Gets the complete component identifiers that a signature must cover.</summary>
    public IReadOnlyList<ComponentIdentifier> RequiredComponents { get; init; } = [];

    /// <summary>Gets whether the signed creation time is required.</summary>
    public bool RequireCreated { get; init; }

    /// <summary>
    /// Gets the maximum accepted age from the signed creation time. Setting this also requires
    /// <c>created</c>.
    /// </summary>
    public TimeSpan? MaximumAge { get; init; }

    /// <summary>Gets whether a present expiration time is enforced.</summary>
    public bool ValidateExpiration { get; init; }

    /// <summary>Gets whether an expiration time is required and enforced.</summary>
    public bool RequireExpires { get; init; }

    /// <summary>Gets the allowed clock skew. The default is zero.</summary>
    public TimeSpan ClockSkew { get; init; }

    /// <summary>Gets an application-specific tag that must be signed, or null for no tag rule.</summary>
    public string? RequiredTag { get; init; }

    /// <summary>Gets whether a nonce is required without recording it in a nonce store.</summary>
    public bool RequireNonce { get; init; }

    /// <summary>
    /// Gets the atomic shared nonce store. When set, a nonce and a finite enforced acceptance
    /// deadline are required.
    /// </summary>
    public INonceStore? NonceStore { get; init; }

    /// <summary>Gets the application-defined replay scope used by <see cref="NonceStore"/>.</summary>
    public string? ReplayScope { get; init; }

    /// <summary>Gets the clock used for timestamp decisions.</summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
}
