// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Tests for signature base construction per RFC 9421 §2.5.
/// Test vectors from RFC 9421 Appendix B.2.
/// </summary>
public class SignatureBaseBuilderTests
{
    // RFC 9421 Appendix B.2 test-request message:
    // POST /foo?param=Value&Pet=dog HTTP/1.1
    // Host: example.com
    // Date: Tue, 20 Apr 2021 02:07:55 GMT
    // Content-Type: application/json
    // Content-Digest: sha-512=:WZDPaVn/7XgHaAy8pmojAkGWoRx2UFChF41A2svX+TaPm+AbwAgBWnrIiYllu7BNNyealdVLvRwEmTHWXvJwew==:
    // Content-Length: 18

    private static TestHttpMessageContext BuildTestRequest()
    {
        var ctx = TestHttpMessageContext.CreateRequest(
            method: "POST",
            scheme: "https",
            authority: "example.com",
            path: "/foo",
            query: "?param=Value&Pet=dog");

        ctx.AddHeader("host", "example.com");
        ctx.AddHeader("date", "Tue, 20 Apr 2021 02:07:55 GMT");
        ctx.AddHeader("content-type", "application/json");
        ctx.AddHeader("content-digest",
            "sha-512=:WZDPaVn/7XgHaAy8pmojAkGWoRx2UFChF41A2svX+TaPm+AbwAgBWnrIiYllu7BNNyealdVLvRwEmTHWXvJwew==:");
        ctx.AddHeader("content-length", "18");

        return ctx;
    }

    // RFC 9421 Appendix B.2 test-response message:
    // HTTP/1.1 200 OK
    // Date: Tue, 20 Apr 2021 02:07:56 GMT
    // Content-Type: application/json
    // Content-Digest: sha-512=:mEWXIS7MaLRuGgxOBdODa3xqM1XdEvxoYhvlCFJ41QJgJc4GTsPp29l5oGX69wWdXymyU0rjJuahq4l5aGgfLQ==:
    // Content-Length: 23

    private static TestHttpMessageContext BuildTestResponse()
    {
        var ctx = TestHttpMessageContext.CreateResponse(200, BuildTestRequest());
        ctx.AddHeader("date", "Tue, 20 Apr 2021 02:07:56 GMT");
        ctx.AddHeader("content-type", "application/json");
        ctx.AddHeader("content-digest",
            "sha-512=:mEWXIS7MaLRuGgxOBdODa3xqM1XdEvxoYhvlCFJ41QJgJc4GTsPp29l5oGX69wWdXymyU0rjJuahq4l5aGgfLQ==:");
        ctx.AddHeader("content-length", "23");
        return ctx;
    }

    /// <summary>
    /// B.2.1: Minimal signature — empty covered components.
    /// </summary>
    [Fact]
    public void Build_B21_MinimalSignature_EmptyCoveredComponents()
    {
        var parameters = new SignatureParameters([])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-key-rsa-pss",
            Nonce = "b3k2pp5k7z-50gnwp.yemd",
        };

        var ctx = BuildTestRequest();
        var result = SignatureBaseBuilder.BuildString(parameters, ctx);

        // RFC 9421 B.2.1 expected signature base:
        var expected =
            "\"@signature-params\": ();created=1618884473;keyid=\"test-key-rsa-pss\";nonce=\"b3k2pp5k7z-50gnwp.yemd\"";

        result.ShouldBe(expected);
    }

    /// <summary>
    /// B.2.2: Selective covered components.
    /// "@authority", "content-digest", "@query-param;name=Pet"
    /// </summary>
    [Fact]
    public void Build_B22_SelectiveCoveredComponents()
    {
        var parameters = new SignatureParameters(
        [
            ComponentIdentifier.Authority,
            ComponentIdentifier.Field("content-digest"),
            ComponentIdentifier.QueryParam("Pet"),
        ])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-key-rsa-pss",
            Tag = "header-example",
        };

        var ctx = BuildTestRequest();
        var result = SignatureBaseBuilder.BuildString(parameters, ctx);

        var expected =
            "\"@authority\": example.com\n" +
            "\"content-digest\": sha-512=:WZDPaVn/7XgHaAy8pmojAkGWoRx2UFChF41A2svX+TaPm+AbwAgBWnrIiYllu7BNNyealdVLvRwEmTHWXvJwew==:\n" +
            "\"@query-param\";name=\"Pet\": dog\n" +
            "\"@signature-params\": (\"@authority\" \"content-digest\" \"@query-param\";name=\"Pet\");created=1618884473;keyid=\"test-key-rsa-pss\";tag=\"header-example\"";

        result.ShouldBe(expected);
    }

    /// <summary>
    /// B.2.3: Full coverage.
    /// "date", "@method", "@path", "@query", "@authority", "content-type", "content-digest", "content-length"
    /// </summary>
    [Fact]
    public void Build_B23_FullCoverage()
    {
        var parameters = new SignatureParameters(
        [
            ComponentIdentifier.Field("date"),
            ComponentIdentifier.Method,
            ComponentIdentifier.Path,
            ComponentIdentifier.Query,
            ComponentIdentifier.Authority,
            ComponentIdentifier.Field("content-type"),
            ComponentIdentifier.Field("content-digest"),
            ComponentIdentifier.Field("content-length"),
        ])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-key-rsa-pss",
        };

        var ctx = BuildTestRequest();
        var result = SignatureBaseBuilder.BuildString(parameters, ctx);

        var expected =
            "\"date\": Tue, 20 Apr 2021 02:07:55 GMT\n" +
            "\"@method\": POST\n" +
            "\"@path\": /foo\n" +
            "\"@query\": ?param=Value&Pet=dog\n" +
            "\"@authority\": example.com\n" +
            "\"content-type\": application/json\n" +
            "\"content-digest\": sha-512=:WZDPaVn/7XgHaAy8pmojAkGWoRx2UFChF41A2svX+TaPm+AbwAgBWnrIiYllu7BNNyealdVLvRwEmTHWXvJwew==:\n" +
            "\"content-length\": 18\n" +
            "\"@signature-params\": (\"date\" \"@method\" \"@path\" \"@query\" \"@authority\" \"content-type\" \"content-digest\" \"content-length\");created=1618884473;keyid=\"test-key-rsa-pss\"";

        result.ShouldBe(expected);
    }

    /// <summary>
    /// B.2.4: Response signing.
    /// "@status", "content-type", "content-digest", "content-length"
    /// </summary>
    [Fact]
    public void Build_B24_SigningResponse()
    {
        var parameters = new SignatureParameters(
        [
            ComponentIdentifier.Status,
            ComponentIdentifier.Field("content-type"),
            ComponentIdentifier.Field("content-digest"),
            ComponentIdentifier.Field("content-length"),
        ])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-key-ecc-p256",
        };

        var ctx = BuildTestResponse();
        var result = SignatureBaseBuilder.BuildString(parameters, ctx);

        var expected =
            "\"@status\": 200\n" +
            "\"content-type\": application/json\n" +
            "\"content-digest\": sha-512=:mEWXIS7MaLRuGgxOBdODa3xqM1XdEvxoYhvlCFJ41QJgJc4GTsPp29l5oGX69wWdXymyU0rjJuahq4l5aGgfLQ==:\n" +
            "\"content-length\": 23\n" +
            "\"@signature-params\": (\"@status\" \"content-type\" \"content-digest\" \"content-length\");created=1618884473;keyid=\"test-key-ecc-p256\"";

        result.ShouldBe(expected);
    }

    /// <summary>
    /// B.2.5: HMAC-SHA256 request signing.
    /// "date", "@authority", "content-type"
    /// </summary>
    [Fact]
    public void Build_B25_HmacSha256Request()
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
        var result = SignatureBaseBuilder.BuildString(parameters, ctx);

        var expected =
            "\"date\": Tue, 20 Apr 2021 02:07:55 GMT\n" +
            "\"@authority\": example.com\n" +
            "\"content-type\": application/json\n" +
            "\"@signature-params\": (\"date\" \"@authority\" \"content-type\");created=1618884473;keyid=\"test-shared-secret\"";

        result.ShouldBe(expected);
    }

    /// <summary>
    /// B.2.6: Ed25519 request signing.
    /// "date", "@method", "@path", "@authority", "content-type", "content-length"
    /// </summary>
    [Fact]
    public void Build_B26_Ed25519Request()
    {
        var parameters = new SignatureParameters(
        [
            ComponentIdentifier.Field("date"),
            ComponentIdentifier.Method,
            ComponentIdentifier.Path,
            ComponentIdentifier.Authority,
            ComponentIdentifier.Field("content-type"),
            ComponentIdentifier.Field("content-length"),
        ])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            KeyId = "test-key-ed25519",
        };

        var ctx = BuildTestRequest();
        var result = SignatureBaseBuilder.BuildString(parameters, ctx);

        var expected =
            "\"date\": Tue, 20 Apr 2021 02:07:55 GMT\n" +
            "\"@method\": POST\n" +
            "\"@path\": /foo\n" +
            "\"@authority\": example.com\n" +
            "\"content-type\": application/json\n" +
            "\"content-length\": 18\n" +
            "\"@signature-params\": (\"date\" \"@method\" \"@path\" \"@authority\" \"content-type\" \"content-length\");created=1618884473;keyid=\"test-key-ed25519\"";

        result.ShouldBe(expected);
    }

    [Fact]
    public void Build_DuplicateComponent_ThrowsSignatureBaseException()
    {
        var parameters = new SignatureParameters(
        [
            ComponentIdentifier.Method,
            ComponentIdentifier.Method, // duplicate
        ]);

        var ctx = BuildTestRequest();

        Should.Throw<SignatureBaseException>(() => SignatureBaseBuilder.BuildString(parameters, ctx));
    }

    [Fact]
    public void Build_DuplicateComponent_DifferentParameterOrder_ThrowsSignatureBaseException()
    {
        // Two identifiers for the same field+parameter set, but built through different code
        // paths, must still be detected as duplicates: RFC 9421 §2.5 duplicate detection is not
        // allowed to depend on wire parameter serialization order.
        var first = new ComponentIdentifier("content-type") { Sf = true, Req = true };
        var wireParameters = new DamianH.Http.StructuredFieldValues.Parameters();
        wireParameters.Add("req", DamianH.Http.StructuredFieldValues.BooleanItem.True);
        wireParameters.Add("sf", DamianH.Http.StructuredFieldValues.BooleanItem.True);
        var second = ComponentIdentifier.FromWire("content-type", wireParameters);

        var parameters = new SignatureParameters([first, second]);
        var ctx = BuildTestRequest();

        Should.Throw<SignatureBaseException>(() => SignatureBaseBuilder.BuildString(parameters, ctx));
    }

    [Fact]
    public void Build_NonAsciiFieldValue_ThrowsSignatureBaseException()
    {
        var ctx = BuildTestRequest();
        ctx.AddHeader("x-non-ascii", "café");
        var parameters = new SignatureParameters([ComponentIdentifier.Field("x-non-ascii")]);

        Should.Throw<SignatureBaseException>(() => SignatureBaseBuilder.BuildString(parameters, ctx));
    }

    [Fact]
    public void Build_TrailerComponent_IncludedInSignatureBase()
    {
        var ctx = TestHttpMessageContext.CreateResponse(200, BuildTestRequest());
        ctx.SetTrailer("expires", "Wed, 9 Nov 2022 07:28:00 GMT");
        var parameters = new SignatureParameters([new ComponentIdentifier("expires") { Tr = true }]);

        var result = SignatureBaseBuilder.BuildString(parameters, ctx);

        result.ShouldBe(
            "\"expires\";tr: Wed, 9 Nov 2022 07:28:00 GMT\n" +
            "\"@signature-params\": (\"expires\";tr)");
    }

    [Fact]
    public void Build_MissingHeaderField_ThrowsSignatureBaseException()
    {
        var parameters = new SignatureParameters([ComponentIdentifier.Field("x-nonexistent")]);
        var ctx = BuildTestRequest();

        Should.Throw<SignatureBaseException>(() => SignatureBaseBuilder.BuildString(parameters, ctx));
    }

    [Fact]
    public void Build_StatusOnRequest_ThrowsSignatureBaseException()
    {
        var parameters = new SignatureParameters([ComponentIdentifier.Status]);
        var ctx = BuildTestRequest(); // a request, not a response

        Should.Throw<SignatureBaseException>(() => SignatureBaseBuilder.BuildString(parameters, ctx));
    }

    [Fact]
    public void Build_MethodOnResponse_ThrowsSignatureBaseException()
    {
        var parameters = new SignatureParameters([ComponentIdentifier.Method]);
        var ctx = BuildTestResponse(); // a response, not a request

        Should.Throw<SignatureBaseException>(() => SignatureBaseBuilder.BuildString(parameters, ctx));
    }

    [Fact]
    public void Build_ReturnsBytes_EquivalentToAsciiEncodedString()
    {
        var parameters = new SignatureParameters([ComponentIdentifier.Method])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
        };

        var ctx = BuildTestRequest();
        var str = SignatureBaseBuilder.BuildString(parameters, ctx);
        var bytes = SignatureBaseBuilder.Build(parameters, ctx);

        System.Text.Encoding.ASCII.GetString(bytes).ShouldBe(str);
    }

    [Fact]
    public void Build_FieldValueWithInjectedLineBreak_ThrowsSignatureBaseException()
    {
        var context = TestHttpMessageContext.CreateRequest(
            "GET", "https", "example.com", "/");
        context.AddHeader("x-value", "first\nsecond");
        var parameters = new SignatureParameters(
            [ComponentIdentifier.Field("x-value")]);

        Should.Throw<SignatureBaseException>(() =>
            SignatureBaseBuilder.Build(parameters, context));
    }
}
