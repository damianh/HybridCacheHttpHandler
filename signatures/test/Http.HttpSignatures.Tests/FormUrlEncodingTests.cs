// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Tests for <see cref="FormUrlEncoding"/>, the application/x-www-form-urlencoded decode/encode
/// helpers used by <see cref="DerivedComponentResolver"/> for <c>@query-param</c> canonicalization
/// per RFC 9421 §2.2.8.
/// </summary>
public sealed class FormUrlEncodingTests
{
    [Theory]
    [InlineData("hello", "hello")]
    [InlineData("with+plus+whitespace", "with plus whitespace")]
    [InlineData("this%20is%20a%20big%0Amultiline%20value", "this is a big\nmultiline value")]
    [InlineData("fa%C3%A7ade%22%3A%20", "façade\": ")]
    public void Decode_MatchesExpected(string encoded, string expected) =>
        FormUrlEncoding.Decode(encoded).ShouldBe(expected);

    [Fact]
    public void Decode_StrayPercentNotFollowedByHex_PassesThroughLiterally() =>
        FormUrlEncoding.Decode("100%").ShouldBe("100%");

    [Fact]
    public void Decode_RawNonAsciiCharacter_Throws() =>
        Should.Throw<FormatException>(() => FormUrlEncoding.Decode("café"));

    [Theory]
    [InlineData("hello", "hello")]
    [InlineData("with plus whitespace", "with%20plus%20whitespace")]
    [InlineData("this is a big\nmultiline value", "this%20is%20a%20big%0Amultiline%20value")]
    [InlineData("façade\": ", "fa%C3%A7ade%22%3A%20")]
    public void Encode_MatchesExpected(string decoded, string expected) =>
        FormUrlEncoding.Encode(decoded).ShouldBe(expected);

    [Fact]
    public void Encode_NeverProducesPlusForSpace() =>
        FormUrlEncoding.Encode(" ").ShouldBe("%20");

    [Theory]
    [InlineData("hello world")]
    [InlineData("with+plus+whitespace")]
    [InlineData("this%20is%20a%20big%0Amultiline%20value")]
    [InlineData("fa%C3%A7ade%22%3A%20")]
    public void Encode_Decode_RoundTripsToCanonicalForm(string original)
    {
        // Applying decode-then-encode a second time to the result must be a no-op: this is what
        // RFC 9421 §2.2.8 relies on when it requires the identifier's 'name' to already be canonical.
        var canonical = FormUrlEncoding.Encode(FormUrlEncoding.Decode(original));
        FormUrlEncoding.Encode(FormUrlEncoding.Decode(canonical)).ShouldBe(canonical);
    }
}
