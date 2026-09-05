// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using DamianH.Http.HttpSignatures.Algorithms;
using DamianH.Http.HttpSignatures.Keys;
using Shouldly;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Integration tests for <see cref="HttpMessageSigner"/>.
/// Uses HMAC-SHA256 (deterministic) for predictable results.
/// </summary>
public sealed class HttpMessageSignerTests
{
    private static readonly byte[] TestKeyBytes =
        Convert.FromBase64String("uzvJfB4u3N0Jy4T7NZ75MDVcr8zSTInedJtkgcu46YW4XByzNJjxBdtjUkdJPBtbmHhIDi6pcl8jsasjlTMtDQ==");

    private static readonly HmacSharedKey TestSigningKey = new("test-shared-secret", TestKeyBytes);
    private static readonly HmacSha256SignatureAlgorithm Algorithm = new();
    private static readonly HttpMessageSigner Signer = new();

    private static TestHttpMessageContext BuildTestRequest()
    {
        var ctx = TestHttpMessageContext.CreateRequest("POST", "https", "example.com", "/foo", "?param=Value&Pet=dog");
        ctx.AddHeader("host", "example.com");
        ctx.AddHeader("date", "Tue, 20 Apr 2021 02:07:55 GMT");
        ctx.AddHeader("content-type", "application/json");
        ctx.AddHeader("content-length", "18");
        return ctx;
    }

    [Fact]
    public void Sign_ProducesSignatureResult()
    {
        var parameters = new SignatureParameters(
        [
            ComponentIdentifier.Field("date"),
            ComponentIdentifier.Authority,
            ComponentIdentifier.Field("content-type"),
        ])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-shared-secret",
        };

        var ctx = BuildTestRequest();
        var result = Signer.Sign("sig1", ctx, parameters, TestSigningKey, Algorithm);

        result.ShouldNotBeNull();
        result.Label.ShouldBe("sig1");
        result.SignatureInputHeaderValue.ShouldStartWith("sig1=");
        result.SignatureHeaderValue.ShouldStartWith("sig1=:");
        result.SignatureHeaderValue.ShouldEndWith(":");
        result.SignatureBytes.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Sign_DeterministicForSameInput()
    {
        var parameters = new SignatureParameters([ComponentIdentifier.Method])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-shared-secret",
        };

        var ctx = BuildTestRequest();
        var result1 = Signer.Sign("sig1", ctx, parameters, TestSigningKey, Algorithm);
        var result2 = Signer.Sign("sig1", ctx, parameters, TestSigningKey, Algorithm);

        result1.SignatureHeaderValue.ShouldBe(result2.SignatureHeaderValue);
    }

    [Fact]
    public void Sign_SignatureInputContainsSerializedParameters()
    {
        var parameters = new SignatureParameters(
        [
            ComponentIdentifier.Field("date"),
            ComponentIdentifier.Authority,
        ])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-shared-secret",
        };

        var ctx = BuildTestRequest();
        var result = Signer.Sign("sig1", ctx, parameters, TestSigningKey, Algorithm);

        result.SignatureInputHeaderValue.ShouldBe(
            "sig1=(\"date\" \"@authority\");created=1618884473;keyid=\"test-shared-secret\"");
    }

    [Fact]
    public void Sign_DifferentLabels_ProduceSameSignature()
    {
        var parameters = new SignatureParameters([ComponentIdentifier.Method])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-shared-secret",
        };

        var ctx = BuildTestRequest();
        var result1 = Signer.Sign("sig1", ctx, parameters, TestSigningKey, Algorithm);
        var result2 = Signer.Sign("sig2", ctx, parameters, TestSigningKey, Algorithm);

        // Same bytes — label doesn't affect signature base
        result1.SignatureBytes.ToArray().ShouldBe(result2.SignatureBytes.ToArray());
        // But different header values (different labels)
        result1.SignatureHeaderValue.ShouldNotBe(result2.SignatureHeaderValue);
    }

    [Fact]
    public void Sign_RejectsConflictingCredentialMetadata()
    {
        var parameters = new SignatureParameters([ComponentIdentifier.Method])
        {
            KeyId = "different-key",
        };

        Should.Throw<ArgumentException>(() =>
            Signer.Sign(
                "sig1",
                BuildTestRequest(),
                parameters,
                new SigningCredentials(TestSigningKey, Algorithm)));
    }
}
