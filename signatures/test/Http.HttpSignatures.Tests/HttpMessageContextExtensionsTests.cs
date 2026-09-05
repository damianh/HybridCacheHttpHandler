// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Tests for <see cref="HttpMessageContextExtensions"/>'s combined-value accessors, which
/// canonicalize raw header/trailer values per RFC 9110 §5.2 (comma-space combining) and
/// §5.5/§9112 §5.1 (OWS trimming and obsolete line-fold unwrapping).
/// </summary>
public sealed class HttpMessageContextExtensionsTests
{
    private static TestHttpMessageContext Context() =>
        TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");

    [Fact]
    public void GetHeaderValue_MissingHeader_ReturnsNull()
    {
        var ctx = Context();
        ctx.GetHeaderValue("x-missing").ShouldBeNull();
    }

    [Fact]
    public void GetHeaderValue_SingleValue_TrimsOptionalWhitespace()
    {
        var ctx = Context();
        ctx.AddHeader("x-example", "  value with internal spaces  ");
        ctx.GetHeaderValue("x-example").ShouldBe("value with internal spaces");
    }

    [Fact]
    public void GetHeaderValue_MultipleValues_CombinesWithCommaSpace()
    {
        var ctx = Context();
        ctx.AddHeader("x-example", "a");
        ctx.AddHeader("x-example", "b");
        ctx.GetHeaderValue("x-example").ShouldBe("a, b");
    }

    [Fact]
    public void GetHeaderValue_ObsoleteLineFolding_CrlfSpUnfoldsToSingleSpace()
    {
        var ctx = Context();
        ctx.AddHeader("x-example", "foo\r\n bar");
        ctx.GetHeaderValue("x-example").ShouldBe("foo bar");
    }

    [Fact]
    public void GetHeaderValue_ObsoleteLineFolding_BareLfHtabUnfoldsToSingleSpace()
    {
        var ctx = Context();
        ctx.AddHeader("x-example", "foo\n\tbar");
        ctx.GetHeaderValue("x-example").ShouldBe("foo bar");
    }

    [Fact]
    public void GetHeaderValue_UnresolvableLineBreak_Throws()
    {
        var ctx = Context();
        // A bare CR/LF not followed by fold whitespace is not obsolete line folding.
        ctx.AddHeader("x-example", "foo\nbar");
        Should.Throw<FormatException>(() => ctx.GetHeaderValue("x-example"));
    }

    [Fact]
    public void GetHeaderValue_IllegalControlCharacter_Throws()
    {
        var ctx = Context();
        ctx.AddHeader("x-example", "foo\u0001bar");
        Should.Throw<FormatException>(() => ctx.GetHeaderValue("x-example"));
    }

    [Fact]
    public void GetTrailerValue_MissingTrailer_ReturnsNull()
    {
        var ctx = Context();
        ctx.GetTrailerValue("expires").ShouldBeNull();
    }

    [Fact]
    public void GetTrailerValue_DoesNotFallBackToHeaderOfSameName()
    {
        var ctx = Context();
        ctx.SetHeader("expires", "header value");
        ctx.GetTrailerValue("expires").ShouldBeNull();
    }

    [Fact]
    public void GetTrailerValue_ReturnsCanonicalizedValue()
    {
        var ctx = Context();
        ctx.AddTrailer("expires", "  Wed, 9 Nov 2022 07:28:00 GMT  ");
        ctx.GetTrailerValue("expires").ShouldBe("Wed, 9 Nov 2022 07:28:00 GMT");
    }
}
