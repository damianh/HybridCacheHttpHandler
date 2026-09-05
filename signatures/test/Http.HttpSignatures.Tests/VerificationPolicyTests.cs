using System.Collections.Concurrent;
using DamianH.Http.HttpSignatures.Algorithms;
using Shouldly;

namespace DamianH.Http.HttpSignatures;

public sealed class VerificationPolicyTests
{
    private static readonly HmacSha256SignatureAlgorithm Algorithm = new();
    private static readonly SigningCredentials SigningCredentials =
        new(RfcTestKeys.HmacSharedSigningKey, Algorithm);
    private static readonly VerificationCredentials VerificationCredentials =
        new(RfcTestKeys.HmacSharedVerificationKey, Algorithm);
    private static readonly HttpMessageSigner Signer = new();
    private static readonly HttpMessageVerifier Verifier = new();
    private static readonly DateTimeOffset Now =
        DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

    [Fact]
    public async Task CryptoValidExpiredSignature_IsNotAcceptedByExpirationPolicy()
    {
        var context = BuildSignedRequest(
            created: Now.AddMinutes(-2),
            expires: Now.AddMinutes(-1));

        var verification = Verifier.Verify("sig1", context, VerificationCredentials);
        var acceptance = await Verifier.VerifyAndValidateAsync(
            "sig1",
            context,
            VerificationCredentials,
            new VerificationPolicy
            {
                ValidateExpiration = true,
                TimeProvider = new FixedTimeProvider(Now),
            });

        verification.IsValid.ShouldBeTrue();
        acceptance.IsAccepted.ShouldBeFalse();
        acceptance.FailureCode.ShouldBe(VerificationAcceptanceFailureCode.SignatureExpired);
    }

    [Fact]
    public async Task DefaultPolicy_DoesNotImplicitlyEnforceTimestampMetadata()
    {
        var context = BuildSignedRequest(
            created: Now.AddDays(1),
            expires: Now.AddDays(-1));

        var acceptance = await Verifier.VerifyAndValidateAsync(
            "sig1",
            context,
            VerificationCredentials,
            new VerificationPolicy { TimeProvider = new FixedTimeProvider(Now) });

        acceptance.IsAccepted.ShouldBeTrue(acceptance.ErrorMessage);
    }

    [Fact]
    public async Task RequiredComponent_UsesCompleteSemanticIdentity()
    {
        var context = BuildSignedRequest(
            created: Now,
            components: [ComponentIdentifier.Field("date")]);

        var acceptance = await Verifier.VerifyAndValidateAsync(
            "sig1",
            context,
            VerificationCredentials,
            new VerificationPolicy
            {
                RequiredComponents = [new ComponentIdentifier("date") { Req = true }],
                TimeProvider = new FixedTimeProvider(Now),
            });

        acceptance.FailureCode.ShouldBe(
            VerificationAcceptanceFailureCode.MissingRequiredComponent);
    }

    [Fact]
    public async Task MaximumAge_IncludesConfiguredClockSkewAtBoundary()
    {
        var context = BuildSignedRequest(created: Now.AddMinutes(-6));

        var acceptance = await Verifier.VerifyAndValidateAsync(
            "sig1",
            context,
            VerificationCredentials,
            new VerificationPolicy
            {
                MaximumAge = TimeSpan.FromMinutes(5),
                ClockSkew = TimeSpan.FromMinutes(1),
                TimeProvider = new FixedTimeProvider(Now),
            });

        acceptance.IsAccepted.ShouldBeTrue(acceptance.ErrorMessage);
    }

    [Fact]
    public async Task ReplayPolicy_ClaimsOnceAndRetainsThroughAcceptanceWindow()
    {
        var context = BuildSignedRequest(
            created: Now.AddMinutes(-1),
            nonce: "unique");
        var store = new AtomicNonceStore();
        var policy = new VerificationPolicy
        {
            MaximumAge = TimeSpan.FromMinutes(5),
            ClockSkew = TimeSpan.FromSeconds(30),
            NonceStore = store,
            ReplayScope = "orders",
            TimeProvider = new FixedTimeProvider(Now),
        };

        var first = await Verifier.VerifyAndValidateAsync(
            "sig1", context, VerificationCredentials, policy);
        var second = await Verifier.VerifyAndValidateAsync(
            "sig1", context, VerificationCredentials, policy);

        first.IsAccepted.ShouldBeTrue(first.ErrorMessage);
        second.FailureCode.ShouldBe(VerificationAcceptanceFailureCode.NonceReplayed);
        store.LastRetainUntil.ShouldBe(Now.AddMinutes(4).AddSeconds(30));
        store.LastCredentialKeyId.ShouldBe("test-shared-secret");
    }

    [Fact]
    public async Task ReplayPolicy_ConcurrentRequestsHaveOneWinner()
    {
        var context = BuildSignedRequest(created: Now, nonce: "race");
        var policy = new VerificationPolicy
        {
            MaximumAge = TimeSpan.FromMinutes(5),
            NonceStore = new AtomicNonceStore(),
            ReplayScope = "orders",
            TimeProvider = new FixedTimeProvider(Now),
        };

        var results = await Task.WhenAll(
            Enumerable.Range(0, 8)
                .Select(_ => Verifier.VerifyAndValidateAsync(
                    "sig1", context, VerificationCredentials, policy).AsTask()));

        results.Count(result => result.IsAccepted).ShouldBe(1);
        results.Count(result =>
            result.FailureCode == VerificationAcceptanceFailureCode.NonceReplayed)
            .ShouldBe(7);
    }

    [Fact]
    public async Task ReplayPolicy_SignatureLabelsDoNotPartitionNonceClaims()
    {
        var context = BuildSignedRequest(created: Now, nonce: "same-nonce");
        var input = context.GetHeaderValue("signature-input")!;
        var signature = context.GetHeaderValue("signature")!;
        context.SetHeader(
            "signature-input",
            $"{input}, {input.Replace("sig1=", "sig2=", StringComparison.Ordinal)}");
        context.SetHeader(
            "signature",
            $"{signature}, {signature.Replace("sig1=", "sig2=", StringComparison.Ordinal)}");
        var policy = new VerificationPolicy
        {
            MaximumAge = TimeSpan.FromMinutes(5),
            NonceStore = new AtomicNonceStore(),
            ReplayScope = "orders",
            TimeProvider = new FixedTimeProvider(Now),
        };

        var first = await Verifier.VerifyAndValidateAsync(
            "sig1", context, VerificationCredentials, policy);
        var second = await Verifier.VerifyAndValidateAsync(
            "sig2", context, VerificationCredentials, policy);

        first.IsAccepted.ShouldBeTrue(first.ErrorMessage);
        second.FailureCode.ShouldBe(VerificationAcceptanceFailureCode.NonceReplayed);
    }

    [Fact]
    public async Task FailedCrypto_DoesNotClaimNonce()
    {
        var context = BuildSignedRequest(created: Now, nonce: "unused");
        context.SetHeader("date", "tampered");
        var store = new AtomicNonceStore();

        var result = await Verifier.VerifyAndValidateAsync(
            "sig1",
            context,
            VerificationCredentials,
            new VerificationPolicy
            {
                MaximumAge = TimeSpan.FromMinutes(5),
                NonceStore = store,
                ReplayScope = "orders",
                TimeProvider = new FixedTimeProvider(Now),
            });

        result.FailureCode.ShouldBe(VerificationAcceptanceFailureCode.VerificationFailed);
        store.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task NonceStoreFailure_PropagatesWithoutAcceptance()
    {
        var context = BuildSignedRequest(created: Now, nonce: "store-error");

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await Verifier.VerifyAndValidateAsync(
                "sig1",
                context,
                VerificationCredentials,
                new VerificationPolicy
                {
                    MaximumAge = TimeSpan.FromMinutes(5),
                    NonceStore = new FailingNonceStore(),
                    ReplayScope = "orders",
                    TimeProvider = new FixedTimeProvider(Now),
                }));
    }

    [Fact]
    public async Task ReplayPolicy_RequiresFiniteEnforcedWindow()
    {
        var context = BuildSignedRequest(created: Now, nonce: "invalid-policy");

        await Should.ThrowAsync<ArgumentException>(async () =>
            await Verifier.VerifyAndValidateAsync(
                "sig1",
                context,
                VerificationCredentials,
                new VerificationPolicy
                {
                    NonceStore = new AtomicNonceStore(),
                    ReplayScope = "orders",
                    TimeProvider = new FixedTimeProvider(Now),
                }));
    }

    private static TestHttpMessageContext BuildSignedRequest(
        DateTimeOffset? created = null,
        DateTimeOffset? expires = null,
        string? nonce = null,
        IReadOnlyList<ComponentIdentifier>? components = null)
    {
        var context = TestHttpMessageContext.CreateRequest(
            "POST", "https", "example.com", "/orders");
        context.AddHeader("date", "Tue, 14 Nov 2023 22:13:20 GMT");

        var parameters = new SignatureParameters(
            components ?? [ComponentIdentifier.Field("date")])
        {
            Created = created,
            Expires = expires,
            Nonce = nonce,
            KeyId = SigningCredentials.Key.KeyId,
            Algorithm = SigningCredentials.Algorithm.AlgorithmName,
        };
        var result = Signer.Sign("sig1", context, parameters, SigningCredentials);
        context.AddHeader("signature-input", result.SignatureInputHeaderValue);
        context.AddHeader("signature", result.SignatureHeaderValue);
        return context;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class AtomicNonceStore : INonceStore
    {
        private readonly ConcurrentDictionary<string, byte> _claims = new();
        private int _callCount;

        public int CallCount => _callCount;
        public DateTimeOffset LastRetainUntil { get; private set; }
        public string? LastCredentialKeyId { get; private set; }

        public ValueTask<bool> TryUseAsync(
            string scope,
            string credentialKeyId,
            string nonce,
            DateTimeOffset retainUntil,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            LastRetainUntil = retainUntil;
            LastCredentialKeyId = credentialKeyId;
            return ValueTask.FromResult(
                _claims.TryAdd($"{scope}\0{credentialKeyId}\0{nonce}", 0));
        }
    }

    private sealed class FailingNonceStore : INonceStore
    {
        public ValueTask<bool> TryUseAsync(
            string scope,
            string credentialKeyId,
            string nonce,
            DateTimeOffset retainUntil,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<bool>(new InvalidOperationException("store unavailable"));
    }
}
