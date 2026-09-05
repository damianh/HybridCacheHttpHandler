namespace DamianH.Http.HttpSignatures;

internal static class VerificationPolicyEvaluator
{
    internal static async ValueTask<VerificationAcceptanceResult> EvaluateAsync(
        VerificationResult verification,
        VerificationPolicy policy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verification);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(policy.RequiredComponents);
        ArgumentNullException.ThrowIfNull(policy.TimeProvider);

        ValidateConfiguration(policy);

        if (!verification.IsValid)
        {
            return VerificationAcceptanceResult.Rejected(
                verification,
                VerificationAcceptanceFailureCode.VerificationFailed,
                verification.ErrorMessage ?? "Signature verification failed.");
        }

        var parameters = verification.Parameters!;
        foreach (var required in policy.RequiredComponents.ToArray())
        {
            ArgumentNullException.ThrowIfNull(required);
            if (!parameters.CoveredComponents.Contains(required))
            {
                return VerificationAcceptanceResult.Rejected(
                    verification,
                    VerificationAcceptanceFailureCode.MissingRequiredComponent,
                    $"Required covered component {required.Serialize()} is absent.");
            }
        }

        var now = policy.TimeProvider.GetUtcNow();
        var created = parameters.Created;
        if ((policy.RequireCreated || policy.MaximumAge.HasValue) && !created.HasValue)
        {
            return VerificationAcceptanceResult.Rejected(
                verification,
                VerificationAcceptanceFailureCode.MissingCreated,
                "The verification policy requires a created signature parameter.");
        }

        var enforceCreated = policy.RequireCreated || policy.MaximumAge.HasValue;
        if (enforceCreated && created.HasValue && created.Value > SafeAdd(now, policy.ClockSkew))
        {
            return VerificationAcceptanceResult.Rejected(
                verification,
                VerificationAcceptanceFailureCode.CreatedInFuture,
                "The signature creation time is later than the allowed clock skew.");
        }

        DateTimeOffset? maximumAgeDeadline = null;
        if (policy.MaximumAge.HasValue)
        {
            maximumAgeDeadline = SafeAdd(
                SafeAdd(created!.Value, policy.MaximumAge.Value),
                policy.ClockSkew);
            if (now > maximumAgeDeadline.Value)
            {
                return VerificationAcceptanceResult.Rejected(
                    verification,
                    VerificationAcceptanceFailureCode.SignatureTooOld,
                    "The signature is older than the configured maximum age.");
            }
        }

        var expires = parameters.Expires;
        var enforceExpires = policy.ValidateExpiration || policy.RequireExpires;
        if (policy.RequireExpires && !expires.HasValue)
        {
            return VerificationAcceptanceResult.Rejected(
                verification,
                VerificationAcceptanceFailureCode.MissingExpires,
                "The verification policy requires an expires signature parameter.");
        }

        DateTimeOffset? expirationDeadline = null;
        if (enforceExpires && expires.HasValue)
        {
            expirationDeadline = SafeAdd(expires.Value, policy.ClockSkew);
            if (now > expirationDeadline.Value)
            {
                return VerificationAcceptanceResult.Rejected(
                    verification,
                    VerificationAcceptanceFailureCode.SignatureExpired,
                    "The signature has expired.");
            }
        }

        if (policy.RequiredTag is not null &&
            !string.Equals(policy.RequiredTag, parameters.Tag, StringComparison.Ordinal))
        {
            return VerificationAcceptanceResult.Rejected(
                verification,
                VerificationAcceptanceFailureCode.TagMismatch,
                $"The signature tag does not match required tag '{policy.RequiredTag}'.");
        }

        var requireNonce = policy.RequireNonce || policy.NonceStore is not null;
        if (requireNonce && parameters.Nonce is null)
        {
            return VerificationAcceptanceResult.Rejected(
                verification,
                VerificationAcceptanceFailureCode.MissingNonce,
                "The verification policy requires a nonce.");
        }

        if (policy.NonceStore is not null)
        {
            var retainUntil = Earliest(maximumAgeDeadline, expirationDeadline);
            if (!retainUntil.HasValue)
            {
                return VerificationAcceptanceResult.Rejected(
                    verification,
                    VerificationAcceptanceFailureCode.MissingExpires,
                    "Replay protection requires an enforced expiration or maximum-age deadline.");
            }

            var claimed = await policy.NonceStore.TryUseAsync(
                policy.ReplayScope!,
                verification.CredentialKeyId!,
                parameters.Nonce!,
                retainUntil.Value,
                cancellationToken);

            if (!claimed)
            {
                return VerificationAcceptanceResult.Rejected(
                    verification,
                    VerificationAcceptanceFailureCode.NonceReplayed,
                    "The signature nonce has already been used.");
            }
        }

        return VerificationAcceptanceResult.Accepted(verification);
    }

    private static void ValidateConfiguration(VerificationPolicy policy)
    {
        if (policy.ClockSkew < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(policy), "ClockSkew cannot be negative.");

        if (policy.MaximumAge.HasValue && policy.MaximumAge.Value <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(policy), "MaximumAge must be positive.");

        if (policy.NonceStore is not null && string.IsNullOrEmpty(policy.ReplayScope))
        {
            throw new ArgumentException(
                "ReplayScope is required when a nonce store is configured.",
                nameof(policy));
        }

        if (policy.NonceStore is not null &&
            !policy.MaximumAge.HasValue &&
            !policy.ValidateExpiration &&
            !policy.RequireExpires)
        {
            throw new ArgumentException(
                "Replay protection requires MaximumAge or enforced expiration.",
                nameof(policy));
        }
    }

    private static DateTimeOffset SafeAdd(DateTimeOffset value, TimeSpan amount)
    {
        var ticks = amount.Ticks;
        if (ticks > 0 && value.UtcTicks > DateTimeOffset.MaxValue.UtcTicks - ticks)
            return DateTimeOffset.MaxValue;
        if (ticks < 0 && value.UtcTicks < DateTimeOffset.MinValue.UtcTicks - ticks)
            return DateTimeOffset.MinValue;
        return value.AddTicks(ticks);
    }

    private static DateTimeOffset? Earliest(DateTimeOffset? first, DateTimeOffset? second)
    {
        if (!first.HasValue) return second;
        if (!second.HasValue) return first;
        return first.Value <= second.Value ? first : second;
    }
}
