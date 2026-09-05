// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Tests for <see cref="DerivedComponentResolver"/> per RFC 9421 §2.2.
/// Exercises each derived component individually.
/// </summary>
public sealed class DerivedComponentResolverTests
{
    private static TestHttpMessageContext BuildRequest(
        string method = "GET",
        string scheme = "https",
        string authority = "example.com",
        string path = "/foo",
        string? query = null) =>
        TestHttpMessageContext.CreateRequest(method, scheme, authority, path, query);

    private static TestHttpMessageContext BuildResponse(int status = 200) =>
        TestHttpMessageContext.CreateResponse(status, BuildRequest());

    // @method
    [Fact]
    public void Resolve_Method_PreservesUppercaseMethod()
    {
        var ctx = BuildRequest(method: "POST");
        var result = DerivedComponentResolver.Resolve(ComponentIdentifier.Method, ctx);
        result.ShouldBe("POST");
    }

    [Fact]
    public void Resolve_Method_PreservesOriginalCaseWithoutTransformation()
    {
        // RFC 9421 §2.2.1: the method name is case sensitive; no transformation is performed
        // on the input method value's case, even though conventional method names are uppercase.
        var ctx = BuildRequest(method: "PoSt");
        var result = DerivedComponentResolver.Resolve(ComponentIdentifier.Method, ctx);
        result.ShouldBe("PoSt");
    }

    [Fact]
    public void Resolve_Method_OnResponse_Throws()
    {
        var ctx = BuildResponse();
        Should.Throw<SignatureBaseException>(
            () => DerivedComponentResolver.Resolve(ComponentIdentifier.Method, ctx));
    }

    // @target-uri
    [Fact]
    public void Resolve_TargetUri_ReturnsFullUri()
    {
        var ctx = BuildRequest(query: "?a=b");
        var result = DerivedComponentResolver.Resolve(ComponentIdentifier.TargetUri, ctx);
        result.ShouldBe("https://example.com/foo?a=b");
    }

    [Fact]
    public void Resolve_TargetUri_OnResponse_Throws()
    {
        var ctx = BuildResponse();
        Should.Throw<SignatureBaseException>(
            () => DerivedComponentResolver.Resolve(ComponentIdentifier.TargetUri, ctx));
    }

    // @authority
    [Fact]
    public void Resolve_Authority_ReturnsLowercase()
    {
        var ctx = BuildRequest(authority: "Example.COM");
        var result = DerivedComponentResolver.Resolve(ComponentIdentifier.Authority, ctx);
        result.ShouldBe("example.com");
    }

    [Fact]
    public void Resolve_Authority_OnResponse_Throws()
    {
        var ctx = BuildResponse();
        Should.Throw<SignatureBaseException>(
            () => DerivedComponentResolver.Resolve(ComponentIdentifier.Authority, ctx));
    }

    // @scheme
    [Fact]
    public void Resolve_Scheme_ReturnsLowercase()
    {
        var ctx = BuildRequest(scheme: "HTTPS");
        var result = DerivedComponentResolver.Resolve(ComponentIdentifier.Scheme, ctx);
        result.ShouldBe("https");
    }

    [Fact]
    public void Resolve_Scheme_OnResponse_Throws()
    {
        var ctx = BuildResponse();
        Should.Throw<SignatureBaseException>(
            () => DerivedComponentResolver.Resolve(ComponentIdentifier.Scheme, ctx));
    }

    // @request-target
    [Fact]
    public void Resolve_RequestTarget_ReturnsPathAndQuery()
    {
        var ctx = BuildRequest(query: "?a=b");
        var result = DerivedComponentResolver.Resolve(ComponentIdentifier.RequestTarget, ctx);
        result.ShouldBe("/foo?a=b");
    }

    [Fact]
    public void Resolve_RequestTarget_NoQuery_ReturnsPath()
    {
        var ctx = BuildRequest();
        var result = DerivedComponentResolver.Resolve(ComponentIdentifier.RequestTarget, ctx);
        result.ShouldBe("/foo");
    }

    // @path
    [Fact]
    public void Resolve_Path_ReturnsPath()
    {
        var ctx = BuildRequest(path: "/bar/baz");
        var result = DerivedComponentResolver.Resolve(ComponentIdentifier.Path, ctx);
        result.ShouldBe("/bar/baz");
    }

    [Fact]
    public void Resolve_Path_EmptyPath_ReturnsSlash()
    {
        var ctx = BuildRequest(path: "");
        var result = DerivedComponentResolver.Resolve(ComponentIdentifier.Path, ctx);
        result.ShouldBe("/");
    }

    // @query
    [Fact]
    public void Resolve_Query_ReturnsQueryWithLeadingQuestionMark()
    {
        var ctx = BuildRequest(query: "?a=b&c=d");
        var result = DerivedComponentResolver.Resolve(ComponentIdentifier.Query, ctx);
        result.ShouldBe("?a=b&c=d");
    }

    [Fact]
    public void Resolve_Query_Absent_ReturnsQuestionMark()
    {
        var ctx = BuildRequest();
        var result = DerivedComponentResolver.Resolve(ComponentIdentifier.Query, ctx);
        result.ShouldBe("?");
    }

    // @query-param
    [Fact]
    public void Resolve_QueryParam_ReturnsParameterValue()
    {
        var ctx = BuildRequest(query: "?param=Value&Pet=dog");
        var id = ComponentIdentifier.QueryParam("Pet");
        var result = DerivedComponentResolver.Resolve(id, ctx);
        result.ShouldBe("dog");
    }

    [Fact]
    public void Resolve_QueryParam_MissingParameter_Throws()
    {
        var ctx = BuildRequest(query: "?a=b");
        var id = ComponentIdentifier.QueryParam("missing");
        Should.Throw<SignatureBaseException>(
            () => DerivedComponentResolver.Resolve(id, ctx));
    }

    [Fact]
    public void Resolve_QueryParam_NoQueryString_Throws()
    {
        var ctx = BuildRequest();
        var id = ComponentIdentifier.QueryParam("x");
        Should.Throw<SignatureBaseException>(
            () => DerivedComponentResolver.Resolve(id, ctx));
    }

    // RFC 9421 §2.2.8 worked example: percent-decode/re-encode round-trips to the same value.
    [Fact]
    public void Resolve_QueryParam_MultilinePercentEncodedValue_RoundTripsUnchanged()
    {
        var ctx = BuildRequest(query: "?var=this%20is%20a%20big%0Amultiline%20value");
        var id = ComponentIdentifier.QueryParam("var");
        var result = DerivedComponentResolver.Resolve(id, ctx);
        result.ShouldBe("this%20is%20a%20big%0Amultiline%20value");
    }

    // RFC 9421 §2.2.8 worked example: '+' decodes to space, which re-encodes as %20, not '+'.
    [Fact]
    public void Resolve_QueryParam_PlusSignValue_ReEncodesAsPercent20()
    {
        var ctx = BuildRequest(query: "?bar=with+plus+whitespace");
        var id = ComponentIdentifier.QueryParam("bar");
        var result = DerivedComponentResolver.Resolve(id, ctx);
        result.ShouldBe("with%20plus%20whitespace");
    }

    // RFC 9421 §2.2.8 worked example: a percent-encoded UTF-8 name round-trips unchanged, and its
    // canonical form is what must be used in the component identifier's 'name' parameter.
    [Fact]
    public void Resolve_QueryParam_PercentEncodedUnicodeName_MatchesCanonicalForm()
    {
        var ctx = BuildRequest(query: "?fa%C3%A7ade%22%3A%20=something");
        var id = ComponentIdentifier.QueryParam("fa%C3%A7ade%22%3A%20");
        var result = DerivedComponentResolver.Resolve(id, ctx);
        result.ShouldBe("something");
    }

    [Fact]
    public void Resolve_QueryParam_EmptyValue_ReturnsEmptyString()
    {
        var ctx = BuildRequest(query: "?flag=&other=x");
        var id = ComponentIdentifier.QueryParam("flag");
        var result = DerivedComponentResolver.Resolve(id, ctx);
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void Resolve_QueryParam_DuplicateNamePostCanonicalization_Throws()
    {
        // "a" and "%61" both canonicalize to "a" — ambiguous, must not be silently resolved.
        var ctx = BuildRequest(query: "?a=1&%61=2");
        var id = ComponentIdentifier.QueryParam("a");
        Should.Throw<SignatureBaseException>(
            () => DerivedComponentResolver.Resolve(id, ctx));
    }

    // @status
    [Fact]
    public void Resolve_Status_ReturnsStatusString()
    {
        var ctx = BuildResponse(200);
        var result = DerivedComponentResolver.Resolve(ComponentIdentifier.Status, ctx);
        result.ShouldBe("200");
    }

    [Fact]
    public void Resolve_Status_404_ReturnsStatusString()
    {
        var ctx = BuildResponse(404);
        var result = DerivedComponentResolver.Resolve(ComponentIdentifier.Status, ctx);
        result.ShouldBe("404");
    }

    [Fact]
    public void Resolve_Status_OnRequest_Throws()
    {
        var ctx = BuildRequest();
        Should.Throw<SignatureBaseException>(
            () => DerivedComponentResolver.Resolve(ComponentIdentifier.Status, ctx));
    }

    // Unknown derived component
    [Fact]
    public void Resolve_UnknownDerived_Throws()
    {
        var id = new ComponentIdentifier("@unknown");
        var ctx = BuildRequest();
        Should.Throw<SignatureBaseException>(
            () => DerivedComponentResolver.Resolve(id, ctx));
    }

    // req parameter
    [Fact]
    public void Resolve_WithReqParameter_ResolvesFromAssociatedRequest()
    {
        var request = BuildRequest(method: "POST");
        var response = TestHttpMessageContext.CreateResponse(200, request);
        var id = new ComponentIdentifier("@method") { Req = true };
        var result = DerivedComponentResolver.Resolve(id, response);
        result.ShouldBe("POST");
    }

    [Fact]
    public void Resolve_WithReqParameter_NoAssociatedRequest_Throws()
    {
        var response = TestHttpMessageContext.CreateResponse(200);
        var id = new ComponentIdentifier("@method") { Req = true };
        Should.Throw<SignatureBaseException>(
            () => DerivedComponentResolver.Resolve(id, response));
    }
}
