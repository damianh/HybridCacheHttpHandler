// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Tests for <see cref="FieldComponentResolver"/> per RFC 9421 §2.1.
/// Exercises default, sf, key, bs, req, and tr resolution.
/// </summary>
public sealed class FieldComponentResolverTests
{
    private static readonly IStructuredFieldTypeResolver Unknown = UnknownStructuredFieldTypeResolver.Instance;

    private static IStructuredFieldTypeResolver DeclareTypes(
        params (string FieldName, StructuredFieldValueKind Kind)[] declarations) =>
        new DictionaryStructuredFieldTypeResolver(declarations.ToDictionary(d => d.FieldName, d => d.Kind));

    // Default: combined field value
    [Fact]
    public void Resolve_DefaultField_ReturnsCombinedValue()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        ctx.AddHeader("content-type", "application/json");
        var id = ComponentIdentifier.Field("content-type");
        var result = FieldComponentResolver.Resolve(id, ctx, Unknown);
        result.ShouldBe("application/json");
    }

    [Fact]
    public void Resolve_DefaultField_MultipleValues_CombinesWithCommaSpace()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        ctx.AddHeader("x-custom", "val1");
        ctx.AddHeader("x-custom", "val2");
        var id = ComponentIdentifier.Field("x-custom");
        var result = FieldComponentResolver.Resolve(id, ctx, Unknown);
        result.ShouldBe("val1, val2");
    }

    [Fact]
    public void Resolve_MissingField_Throws()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        var id = ComponentIdentifier.Field("x-nonexistent");
        Should.Throw<SignatureBaseException>(
            () => FieldComponentResolver.Resolve(id, ctx, Unknown));
    }

    // sf parameter: strict structured field serialization
    [Fact]
    public void Resolve_SfItem_ReSerializesCanonically()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        // Token item with extra whitespace — SF canonical form is without extra space
        ctx.AddHeader("example-item", "  token  ");
        var id = ComponentIdentifier.FieldSf("example-item");
        var resolver = DeclareTypes(("example-item", StructuredFieldValueKind.Item));
        // Should be parsed and re-serialized canonically
        var result = FieldComponentResolver.Resolve(id, ctx, resolver);
        result.ShouldBe("token");
    }

    [Fact]
    public void Resolve_SfDictionary_ReSerializesCanonically()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        ctx.AddHeader("example-dict", "a=1,  b=2");
        var id = ComponentIdentifier.FieldSf("example-dict");
        var resolver = DeclareTypes(("example-dict", StructuredFieldValueKind.Dictionary));
        var result = FieldComponentResolver.Resolve(id, ctx, resolver);
        result.ShouldBe("a=1, b=2");
    }

    [Fact]
    public void Resolve_Sf_InvalidValue_Throws()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        ctx.AddHeader("bad-sf", "{{{{not valid}}}}");
        var id = ComponentIdentifier.FieldSf("bad-sf");
        var resolver = DeclareTypes(("bad-sf", StructuredFieldValueKind.Item));
        Should.Throw<SignatureBaseException>(
            () => FieldComponentResolver.Resolve(id, ctx, resolver));
    }

    [Fact]
    public void Resolve_Sf_UndeclaredFieldType_Throws()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        ctx.AddHeader("example-item", "token");
        var id = ComponentIdentifier.FieldSf("example-item");
        Should.Throw<SignatureBaseException>(
            () => FieldComponentResolver.Resolve(id, ctx, Unknown));
    }

    // key parameter: dictionary member extraction
    [Fact]
    public void Resolve_DictionaryKey_ReturnsSerializedMemberValue()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        ctx.AddHeader("example-dict", "a=1, b=2, c=3");
        var id = ComponentIdentifier.FieldKey("example-dict", "b");
        var resolver = DeclareTypes(("example-dict", StructuredFieldValueKind.Dictionary));
        var result = FieldComponentResolver.Resolve(id, ctx, resolver);
        result.ShouldBe("2");
    }

    [Fact]
    public void Resolve_DictionaryKey_InnerListUsesCanonicalSharedWriter()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        ctx.AddHeader("example-dict", "a=(\"hello world\";flag=?1 @0 %\"caf%c3%a9\");q=1.000");
        var resolver = DeclareTypes(("example-dict", StructuredFieldValueKind.Dictionary));

        var result = FieldComponentResolver.Resolve(ComponentIdentifier.FieldKey("example-dict", "a"), ctx, resolver);

        result.ShouldBe("(\"hello world\";flag @0 %\"caf%c3%a9\");q=1.0");
    }

    [Fact]
    public void Resolve_DictionaryKey_MalformedKeyReportsSignatureBaseException()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        ctx.AddHeader("example-dict", "_invalid=1");
        var resolver = DeclareTypes(("example-dict", StructuredFieldValueKind.Dictionary));

        Should.Throw<SignatureBaseException>(() =>
            FieldComponentResolver.Resolve(ComponentIdentifier.FieldKey("example-dict", "a"), ctx, resolver));
    }

    [Fact]
    public void Resolve_DictionaryKey_MissingKey_Throws()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        ctx.AddHeader("example-dict", "a=1, b=2");
        var id = ComponentIdentifier.FieldKey("example-dict", "z");
        var resolver = DeclareTypes(("example-dict", StructuredFieldValueKind.Dictionary));
        Should.Throw<SignatureBaseException>(
            () => FieldComponentResolver.Resolve(id, ctx, resolver));
    }

    [Fact]
    public void Resolve_DictionaryKey_NotDictionary_Throws()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        ctx.AddHeader("not-dict", "just a value");
        var id = ComponentIdentifier.FieldKey("not-dict", "x");
        var resolver = DeclareTypes(("not-dict", StructuredFieldValueKind.Item));
        Should.Throw<SignatureBaseException>(
            () => FieldComponentResolver.Resolve(id, ctx, resolver));
    }

    [Fact]
    public void Resolve_DictionaryKey_UndeclaredFieldType_Throws()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        ctx.AddHeader("example-dict", "a=1, b=2");
        var id = ComponentIdentifier.FieldKey("example-dict", "a");
        Should.Throw<SignatureBaseException>(
            () => FieldComponentResolver.Resolve(id, ctx, Unknown));
    }

    // bs parameter: binary-wrapped
    [Fact]
    public void Resolve_Bs_SingleValue_WrapsAsByteSequence()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        ctx.AddHeader("x-example", "hello");
        var id = ComponentIdentifier.FieldBs("x-example");
        var result = FieldComponentResolver.Resolve(id, ctx, Unknown);
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
        var result = FieldComponentResolver.Resolve(id, ctx, Unknown);
        // Each value wrapped separately, combined with ", "
        result.ShouldBe(":Zm9v:, :YmFy:");
    }

    [Fact]
    public void Resolve_Bs_CanonicalizesEachRawValueBeforeWrapping()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        ctx.AddHeader("x-example", " \thello\r\n\tworld  ");

        var result = FieldComponentResolver.Resolve(
            ComponentIdentifier.FieldBs("x-example"),
            ctx,
            Unknown);

        result.ShouldBe(":aGVsbG8gd29ybGQ=:");
    }

    [Fact]
    public void Resolve_Bs_MissingField_Throws()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        var id = ComponentIdentifier.FieldBs("x-missing");
        Should.Throw<SignatureBaseException>(
            () => FieldComponentResolver.Resolve(id, ctx, Unknown));
    }

    [Fact]
    public void Resolve_Bs_NonLatin1Character_Throws()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        // U+0100 cannot be represented losslessly as a single Latin-1 byte.
        ctx.AddHeader("x-example", "caf\u0100");
        var id = ComponentIdentifier.FieldBs("x-example");
        Should.Throw<SignatureBaseException>(
            () => FieldComponentResolver.Resolve(id, ctx, Unknown));
    }

    // req parameter: resolution from associated request
    [Fact]
    public void Resolve_ReqParameter_ResolvesFromAssociatedRequest()
    {
        var request = TestHttpMessageContext.CreateRequest("POST", "https", "example.com", "/");
        request.AddHeader("content-type", "application/json");
        var response = TestHttpMessageContext.CreateResponse(200, request);
        var id = new ComponentIdentifier("content-type") { Req = true };
        var result = FieldComponentResolver.Resolve(id, response, Unknown);
        result.ShouldBe("application/json");
    }

    [Fact]
    public void Resolve_ReqParameter_NoAssociatedRequest_Throws()
    {
        var response = TestHttpMessageContext.CreateResponse(200);
        var id = new ComponentIdentifier("content-type") { Req = true };
        Should.Throw<SignatureBaseException>(
            () => FieldComponentResolver.Resolve(id, response, Unknown));
    }

    [Fact]
    public void Resolve_ReqParameter_OnRequestMessage_Throws()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        ctx.AddHeader("content-type", "application/json");
        var id = new ComponentIdentifier("content-type") { Req = true };
        Should.Throw<SignatureBaseException>(
            () => FieldComponentResolver.Resolve(id, ctx, Unknown));
    }

    // Case-insensitive header lookup
    [Fact]
    public void Resolve_FieldName_IsCaseInsensitive()
    {
        var ctx = TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");
        ctx.AddHeader("Content-Type", "text/html");
        var id = ComponentIdentifier.Field("content-type");
        var result = FieldComponentResolver.Resolve(id, ctx, Unknown);
        result.ShouldBe("text/html");
    }

    // tr parameter: trailer field lookup
    [Fact]
    public void Resolve_TrParameter_ResolvesFromTrailer()
    {
        var ctx = TestHttpMessageContext.CreateResponse(200);
        ctx.SetTrailer("expires", "Wed, 9 Nov 2022 07:28:00 GMT");
        var id = new ComponentIdentifier("expires") { Tr = true };
        var result = FieldComponentResolver.Resolve(id, ctx, Unknown);
        result.ShouldBe("Wed, 9 Nov 2022 07:28:00 GMT");
    }

    [Fact]
    public void Resolve_TrParameter_DoesNotFallBackToHeader()
    {
        var ctx = TestHttpMessageContext.CreateResponse(200);
        ctx.SetHeader("expires", "this is a header, not a trailer");
        var id = new ComponentIdentifier("expires") { Tr = true };
        Should.Throw<SignatureBaseException>(
            () => FieldComponentResolver.Resolve(id, ctx, Unknown));
    }

    [Fact]
    public void Resolve_TrParameter_MissingTrailer_Throws()
    {
        var ctx = TestHttpMessageContext.CreateResponse(200);
        var id = new ComponentIdentifier("expires") { Tr = true };
        Should.Throw<SignatureBaseException>(
            () => FieldComponentResolver.Resolve(id, ctx, Unknown));
    }
}
