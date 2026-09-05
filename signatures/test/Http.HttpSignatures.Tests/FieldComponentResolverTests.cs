// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Tests for <see cref="FieldComponentResolver"/> per RFC 9421 §2.1.
/// Exercises default, sf, key, bs, and req resolution.
/// </summary>
public sealed class FieldComponentResolverTests
{
    // Default: combined field value
    [Fact]
    public void Resolve_DefaultField_ReturnsCombinedValue()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        ctx.AddHeader("content-type", "application/json");
        var id = ComponentIdentifier.Field("content-type");
        var result = FieldComponentResolver.Resolve(id, ctx);
        result.ShouldBe("application/json");
    }

    [Fact]
    public void Resolve_DefaultField_MultipleValues_CombinesWithCommaSpace()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        ctx.AddHeader("x-custom", "val1");
        ctx.AddHeader("x-custom", "val2");
        var id = ComponentIdentifier.Field("x-custom");
        var result = FieldComponentResolver.Resolve(id, ctx);
        result.ShouldBe("val1, val2");
    }

    [Fact]
    public void Resolve_MissingField_Throws()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        var id = ComponentIdentifier.Field("x-nonexistent");
        Should.Throw<SignatureBaseException>(
            () => FieldComponentResolver.Resolve(id, ctx));
    }

    // sf parameter: strict structured field serialization
    [Fact]
    public void Resolve_SfItem_ReSerializesCanonically()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        // Token item with extra whitespace — SF canonical form is without extra space
        ctx.AddHeader("example-item", "  token  ");
        var id = ComponentIdentifier.FieldSf("example-item");
        // Should be parsed and re-serialized canonically
        var result = FieldComponentResolver.Resolve(id, ctx);
        result.ShouldBe("token");
    }

    [Fact]
    public void Resolve_SfDictionary_ReSerializesCanonically()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        ctx.AddHeader("example-dict", "a=1,  b=2");
        var id = ComponentIdentifier.FieldSf("example-dict");
        var result = FieldComponentResolver.Resolve(id, ctx);
        result.ShouldBe("a=1, b=2");
    }

    [Fact]
    public void Resolve_Sf_InvalidValue_Throws()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        ctx.AddHeader("bad-sf", "{{{{not valid}}}}");
        var id = ComponentIdentifier.FieldSf("bad-sf");
        Should.Throw<SignatureBaseException>(
            () => FieldComponentResolver.Resolve(id, ctx));
    }

    // key parameter: dictionary member extraction
    [Fact]
    public void Resolve_DictionaryKey_ReturnsSerializedMemberValue()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        ctx.AddHeader("example-dict", "a=1, b=2, c=3");
        var id = ComponentIdentifier.FieldKey("example-dict", "b");
        var result = FieldComponentResolver.Resolve(id, ctx);
        result.ShouldBe("2");
    }

    [Fact]
    public void Resolve_DictionaryKey_InnerListUsesCanonicalSharedWriter()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        ctx.AddHeader("example-dict", "a=(\"hello world\";flag=?1 @0 %\"caf%c3%a9\");q=1.000");

        var result = FieldComponentResolver.Resolve(ComponentIdentifier.FieldKey("example-dict", "a"), ctx);

        result.ShouldBe("(\"hello world\";flag @0 %\"caf%c3%a9\");q=1.0");
    }

    [Fact]
    public void Resolve_DictionaryKey_MalformedKeyReportsSignatureBaseException()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        ctx.AddHeader("example-dict", "_invalid=1");

        Should.Throw<SignatureBaseException>(() =>
            FieldComponentResolver.Resolve(ComponentIdentifier.FieldKey("example-dict", "a"), ctx));
    }

    [Fact]
    public void Resolve_DictionaryKey_MissingKey_Throws()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        ctx.AddHeader("example-dict", "a=1, b=2");
        var id = ComponentIdentifier.FieldKey("example-dict", "z");
        Should.Throw<SignatureBaseException>(
            () => FieldComponentResolver.Resolve(id, ctx));
    }

    [Fact]
    public void Resolve_DictionaryKey_NotDictionary_Throws()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        ctx.AddHeader("not-dict", "just a value");
        var id = ComponentIdentifier.FieldKey("not-dict", "x");
        Should.Throw<SignatureBaseException>(
            () => FieldComponentResolver.Resolve(id, ctx));
    }

    // bs parameter: binary-wrapped
    [Fact]
    public void Resolve_Bs_SingleValue_WrapsAsByteSequence()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        ctx.AddHeader("x-example", "hello");
        var id = ComponentIdentifier.FieldBs("x-example");
        var result = FieldComponentResolver.Resolve(id, ctx);
        // "hello" → Latin-1 bytes → base64 → SF Byte Sequence :aGVsbG8=:
        result.ShouldBe(":aGVsbG8=:");
    }

    [Fact]
    public void Resolve_Bs_MultipleValues_CombinesWithCommaSpace()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        ctx.AddHeader("x-multi", "foo");
        ctx.AddHeader("x-multi", "bar");
        var id = ComponentIdentifier.FieldBs("x-multi");
        var result = FieldComponentResolver.Resolve(id, ctx);
        // Each value wrapped separately, combined with ", "
        result.ShouldBe(":Zm9v:, :YmFy:");
    }

    [Fact]
    public void Resolve_Bs_MissingField_Throws()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        var id = ComponentIdentifier.FieldBs("x-missing");
        Should.Throw<SignatureBaseException>(
            () => FieldComponentResolver.Resolve(id, ctx));
    }

    // req parameter: resolution from associated request
    [Fact]
    public void Resolve_ReqParameter_ResolvesFromAssociatedRequest()
    {
        var request = TestHttpMessageContext.CreateRequest("POST", "https", "example.com", "/");
        request.AddHeader("content-type", "application/json");
        var response = TestHttpMessageContext.CreateResponse(200, request);
        var id = new ComponentIdentifier("content-type") { Req = true };
        var result = FieldComponentResolver.Resolve(id, response);
        result.ShouldBe("application/json");
    }

    [Fact]
    public void Resolve_ReqParameter_NoAssociatedRequest_Throws()
    {
        var response = TestHttpMessageContext.CreateResponse(200);
        var id = new ComponentIdentifier("content-type") { Req = true };
        Should.Throw<SignatureBaseException>(
            () => FieldComponentResolver.Resolve(id, response));
    }

    // Case-insensitive header lookup
    [Fact]
    public void Resolve_FieldName_IsCaseInsensitive()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        ctx.AddHeader("Content-Type", "text/html");
        var id = ComponentIdentifier.Field("content-type");
        var result = FieldComponentResolver.Resolve(id, ctx);
        result.ShouldBe("text/html");
    }
}
