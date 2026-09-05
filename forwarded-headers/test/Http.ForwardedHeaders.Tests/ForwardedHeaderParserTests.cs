// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.ForwardedHeaders;

public sealed class ForwardedHeaderParserTests
{
    [Fact]
    public void PublishedRfcExamplesPreserveHopAssociations()
    {
        var single = ForwardedHeaderParser.Parse("for=192.0.2.43");
        single.Elements.Single().For.ShouldBe("192.0.2.43");

        var obfuscated = ForwardedHeaderParser.Parse("for=\"_gazonk\"");
        obfuscated.Elements.Single().For.ShouldBe("_gazonk");

        var chain = ForwardedHeaderParser.Parse("For=192.0.2.43, for=198.51.100.17");
        chain.Elements.Select(element => element.For).ShouldBe(["192.0.2.43", "198.51.100.17"]);

        var ipv6 = ForwardedHeaderParser.Parse("for=192.0.2.43,for=\"[2001:db8:cafe::17]\",for=unknown");
        ipv6.Elements.Select(element => element.For)
            .ShouldBe(["192.0.2.43", "[2001:db8:cafe::17]", "unknown"]);

        var complete = ForwardedHeaderParser.Parse(
            "for=192.0.2.60;proto=http;by=203.0.113.43, for=\"[2001:db8:cafe::17]:4711\"");
        complete.Elements[0].For.ShouldBe("192.0.2.60");
        complete.Elements[0].Proto.ShouldBe("http");
        complete.Elements[0].By.ShouldBe("203.0.113.43");
        complete.Elements[1].For.ShouldBe("[2001:db8:cafe::17]:4711");
        complete.Elements[1].Proto.ShouldBeNull();
        complete.Elements[1].By.ShouldBeNull();

        var host = ForwardedHeaderParser.Parse("for=192.0.2.43;host=\"example.com:443\";proto=https");
        host.Elements.Single().Host.ShouldBe("example.com:443");
    }

    [Fact]
    public void MultipleFieldsAreEquivalentToCommaJoiningAndEnumeratedOnce()
    {
        string?[] fields = [" for=192.0.2.43;ext=\"a,b\" ", null, "", "\tfor=\"[::1]:80\";PROTO=https "];
        var enumerations = 0;
        IEnumerable<string?> Enumerate()
        {
            enumerations++;
            if (enumerations > 1)
            {
                throw new InvalidOperationException("The field source was enumerated twice.");
            }

            foreach (var field in fields)
            {
                yield return field;
            }
        }

        ForwardedHeaderParser.TryParse(Enumerate(), out var multiple, out var error).ShouldBeTrue();
        multiple.ShouldNotBeNull();
        error.ShouldBeNull();
        enumerations.ShouldBe(1);

        var joined = ForwardedHeaderParser.Parse(string.Join(",", fields));
        multiple.Value.ShouldBe(joined.Value);
        multiple.Elements.Count.ShouldBe(joined.Elements.Count);
        for (var index = 0; index < joined.Elements.Count; index++)
        {
            multiple.Elements[index].Parameters.ShouldBe(joined.Elements[index].Parameters);
            multiple.Elements[index].PrefixLength.ShouldBe(joined.Elements[index].PrefixLength);
        }

        ForwardedHeaderParser.Parse(fields).Value.ShouldBe(joined.Value);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData(" \t", 0)]
    [InlineData(",", 0)]
    [InlineData(" , ,\t,, ", 0)]
    [InlineData(";", 1)]
    [InlineData(";;;", 1)]
    [InlineData(" ;;; \t", 1)]
    [InlineData(";,;", 2)]
    [InlineData(",;,", 1)]
    [InlineData(" ,for=a, ,for=b,, ", 2)]
    [InlineData(";for=a;;by=b;", 1)]
    [InlineData("for=a;;by=b;;", 1)]
    public void EmptyListMembersAndSemicolonSlotsFollowTheGrammar(string value, int count)
    {
        var header = ForwardedHeaderParser.Parse(value);
        header.Value.ShouldBe(value);
        header.Elements.Count.ShouldBe(count);
        if (header.Elements.Count != 0)
        {
            header.Elements[0].PrefixLength.ShouldBe(0);
        }
    }

    [Fact]
    public void SemicolonOnlyElementRemainsAnExplicitTrustBoundary()
    {
        var header = ForwardedHeaderParser.Parse(",for=far, ,;;,\tfor=near,");

        header.Elements.Count.ShouldBe(3);
        header.Elements[1].Parameters.ShouldBeEmpty();
        header.Elements[1].For.ShouldBeNull();
        header.Value[..header.Elements[1].PrefixLength].ShouldBe(",for=far, ");
        header.Value[..header.Elements[2].PrefixLength].ShouldBe(",for=far, ,;;");
    }

    [Theory]
    [InlineData("for=far,for=near", "for=far")]
    [InlineData(", ,for=far,for=near", ", ,for=far")]
    [InlineData("for=far \t,\tfor=near", "for=far \t")]
    [InlineData("for=far,, ,\tfor=near", "for=far,, ")]
    [InlineData("for=far;ext=\"a,b;c\",for=near", "for=far;ext=\"a,b;c\"")]
    public void PrefixLengthPreservesExactlyTheOriginalUnconsumedPrefix(string value, string expected)
    {
        var header = ForwardedHeaderParser.Parse(value);
        header.Value[..header.Elements[1].PrefixLength].ShouldBe(expected);
        ForwardedHeaderParser.Parse(expected).Elements.Single().For.ShouldBe("far");
        header.Value[..header.Elements[0].PrefixLength].ShouldBeEmpty();
    }

    [Fact]
    public void LeadingAndTrailingEmptyFieldsDoNotRetainConsumedHops()
    {
        var header = ForwardedHeaderParser.Parse(new string?[] { null, "", " \tfor=a ", "", "for=b\t", "", null });
        header.Elements.Count.ShouldBe(2);
        header.Elements[0].PrefixLength.ShouldBe(0);
        var remaining = header.Value[..header.Elements[1].PrefixLength];
        remaining.ShouldBe(",, \tfor=a ,");
        ForwardedHeaderParser.Parse(remaining).Elements.Single().For.ShouldBe("a");
    }

    [Fact]
    public void NamesAreCaseInsensitiveAndUnknownExtensionsAreRetained()
    {
        var header = ForwardedHeaderParser.Parse("FoR=UNKNOWN;bY=_Proxy;HoSt=Example.COM;pRoTo=HtTpS;X-Custom=MiXeD");
        var element = header.Elements.Single();

        element.For.ShouldBe("UNKNOWN");
        element.By.ShouldBe("_Proxy");
        element.Host.ShouldBe("Example.COM");
        element.Proto.ShouldBe("HtTpS");
        element.Parameters["x-custom"].ShouldBe("MiXeD");
        element.Parameters.Keys.ShouldContain("X-Custom");
        element.Parameters.Count.ShouldBe(5);
    }

    [Fact]
    public void AllHttpTokenCharactersAreAcceptedInNamesAndValues()
    {
        const string token = "!#$%&'*+-.^_`|~0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        var element = ForwardedHeaderParser.Parse($"{token}={token}").Elements.Single();
        element.Parameters.Single().Key.ShouldBe(token);
        element.Parameters.Single().Value.ShouldBe(token);
    }

    [Theory]
    [InlineData("x=\"\"", "")]
    [InlineData("x=\"a,b;c=d\"", "a,b;c=d")]
    [InlineData("x=\"a\\\"b\"", "a\"b")]
    [InlineData("x=\"a\\\\b\"", "a\\b")]
    [InlineData("x=\"\\a\\b\\c\"", "abc")]
    [InlineData("x=\"a\\,b\\;c\\=d\"", "a,b;c=d")]
    [InlineData("x=\"a\t b\"", "a\t b")]
    [InlineData("x=\"\\ \\\t\"", " \t")]
    [InlineData("x=\"\u0080\u00a0\u00ff\"", "\u0080\u00a0\u00ff")]
    [InlineData("x=\"\\\u0080\\\u00ff\"", "\u0080\u00ff")]
    public void QuotedStringsDecodeOnlyQuotedPairs(string value, string expected)
    {
        ForwardedHeaderParser.Parse(value).Elements.Single().Parameters["x"].ShouldBe(expected);
    }

    [Fact]
    public void EveryPermittedQuotedOctetWorksLiteralOrEscaped()
    {
        for (var code = 0; code <= 255; code++)
        {
            var character = (char)code;
            var isQuotedText = code == 9 || code == 32 || code == 33 ||
                code is >= 35 and <= 91 or >= 93 and <= 126 or >= 128 and <= 255;
            var isQuotedPair = code == 9 || code is >= 32 and <= 126 or >= 128 and <= 255;

            ForwardedHeaderParser.TryParse($"x=\"{character}\"", out var literalHeader, out _)
                .ShouldBe(isQuotedText, $"Literal octet {code}");
            if (isQuotedText)
            {
                literalHeader!.Elements.Single().Parameters["x"].ShouldBe(character.ToString());
            }

            ForwardedHeaderParser.TryParse($"x=\"\\{character}\"", out var escapedHeader, out _)
                .ShouldBe(isQuotedPair, $"Escaped octet {code}");
            if (isQuotedPair)
            {
                escapedHeader!.Elements.Single().Parameters["x"].ShouldBe(character.ToString());
            }
        }
    }

    [Theory]
    [InlineData("for =a")]
    [InlineData("for\t=a")]
    [InlineData("for= a")]
    [InlineData("for=\ta")]
    [InlineData("for=a ;by=b")]
    [InlineData("for=a;\tby=b")]
    [InlineData("for=a; by=b")]
    [InlineData("for=a ;")]
    [InlineData("; ;")]
    [InlineData("for=a by=b")]
    [InlineData("for=\"a\" by=b")]
    [InlineData("for=\"a\"\t;by=b")]
    [InlineData("\u00a0for=a")]
    [InlineData("for=a\u00a0")]
    public void WhitespaceIsAllowedOnlyAtListBoundariesAndInsideQuotes(string value)
    {
        AssertMalformed(value);
    }

    [Theory]
    [InlineData("for")]
    [InlineData("for=")]
    [InlineData("for=,")]
    [InlineData("for=;")]
    [InlineData("=a")]
    [InlineData("for==a")]
    [InlineData("for=a=foo")]
    [InlineData("for=a:80")]
    [InlineData("for=[::1]")]
    [InlineData("for=http://example.com")]
    [InlineData("f@r=a")]
    [InlineData("for=a/b")]
    [InlineData("for=a\\b")]
    [InlineData("for=\"unterminated")]
    [InlineData("for=\"unterminated\\")]
    [InlineData("for=\"x\"\"y\"")]
    [InlineData("for=\"x\"trailing")]
    [InlineData("for=\"x\"=trailing")]
    [InlineData("for=\u00e9")]
    [InlineData("\u00e9=value")]
    [InlineData("for=\"\u0100\"")]
    [InlineData("for=\"\\\u0100\"")]
    [InlineData("for=\"\ud800\"")]
    [InlineData("for=a\r\nfor=b")]
    [InlineData("for=\"a\r\n b\"")]
    [InlineData("for=\"a\\\r\n b\"")]
    public void MalformedTokensQuotesAndDelimitersAreRejected(string value)
    {
        AssertMalformed(value);
    }

    [Fact]
    public void ForbiddenControlCharactersAreRejectedAtEveryStructuralPosition()
    {
        var controls = Enumerable.Range(0, 32).Where(value => value != 9).Append(127);
        foreach (var code in controls)
        {
            var character = (char)code;
            string[] values =
            [
                $"{character}for=a", $"for{character}=a", $"for={character}a",
                $"for=a{character}", $"for=a,{character}for=b", $"for=a;{character}by=b",
                $"for=\"a{character}b\"", $"for=\"a\\{character}b\""
            ];
            foreach (var value in values)
            {
                AssertMalformed(value);
            }
        }
    }

    [Theory]
    [InlineData("for=a;for=b", 6)]
    [InlineData("For=a;fOr=b", 6)]
    [InlineData("proto=a;PROTO=b", 8)]
    [InlineData("x-custom=a;X-CUSTOM=b", 11)]
    [InlineData("x=\"one\";;X=\"two\"", 9)]
    public void DuplicateNamesAreRejectedCaseInsensitivelyIncludingExtensions(string value, int position)
    {
        ForwardedHeaderParser.TryParse(value, out var header, out var error).ShouldBeFalse();
        header.ShouldBeNull();
        error.ShouldNotBeNull();
        error.Position.ShouldBe(position);
        error.Message.ShouldContain("more than once");
    }

    [Fact]
    public void ParametersMayRepeatAcrossElements()
    {
        var header = ForwardedHeaderParser.Parse("For=a;X=one,for=b;x=two");
        header.Elements.Select(element => element.Parameters["x"]).ShouldBe(["one", "two"]);
    }

    [Theory]
    [InlineData("for=", 4)]
    [InlineData("for=\"a", 6)]
    [InlineData("for=\"a\\", 7)]
    [InlineData("for =a", 3)]
    [InlineData("for= a", 4)]
    [InlineData("for=a\n", 5)]
    [InlineData("for=\"a\r\"", 6)]
    [InlineData("for=\"\\\r\"", 6)]
    public void ErrorsReportTheExactFailureOffset(string value, int position)
    {
        ForwardedHeaderParser.TryParse(value, out _, out var error).ShouldBeFalse();
        error.ShouldNotBeNull();
        error.Position.ShouldBe(position);
    }

    [Fact]
    public void MultiFieldErrorsUseTheJoinedOffset()
    {
        ForwardedHeaderParser.TryParse(new[] { "for=a", "for=" }, out var header, out var error).ShouldBeFalse();
        header.ShouldBeNull();
        error.ShouldNotBeNull();
        error.Position.ShouldBe(10);
    }

    [Fact]
    public void ErrorsDoNotEchoInputAndThrowingParseUsesTheSameDiagnostic()
    {
        const string value = "private-client-identity=\"private-token\";PRIVATE-CLIENT-IDENTITY=x";
        ForwardedHeaderParser.TryParse(value, out _, out var error).ShouldBeFalse();
        error.ShouldNotBeNull();
        error.Message.ShouldNotContain("private", Case.Insensitive);

        var exception = Should.Throw<FormatException>(() => ForwardedHeaderParser.Parse(value));
        exception.Message.ShouldContain(error.Message);
        exception.Message.ShouldContain(error.Position.ToString());
        exception.Message.ShouldNotContain("private", Case.Insensitive);

        var multiException = Should.Throw<FormatException>(() => ForwardedHeaderParser.Parse(new[] { value }));
        multiException.Message.ShouldBe(exception.Message);
    }

    [Fact]
    public void NullInputIsAProgrammerErrorButNullFieldMembersAreEmpty()
    {
        Should.Throw<ArgumentNullException>(() => ForwardedHeaderParser.Parse((string)null!));
        Should.Throw<ArgumentNullException>(() => ForwardedHeaderParser.Parse((IEnumerable<string?>)null!));
        Should.Throw<ArgumentNullException>(() => ForwardedHeaderParser.TryParse((string)null!, out _, out _));
        Should.Throw<ArgumentNullException>(() =>
            ForwardedHeaderParser.TryParse((IEnumerable<string?>)null!, out _, out _));
        ForwardedHeaderParser.Parse(new string?[] { null, null }).Value.ShouldBe(",");
        ForwardedHeaderParser.Parse(Array.Empty<string?>()).Elements.ShouldBeEmpty();
    }

    [Fact]
    public void SyntaxParsingDoesNotValidateTheSemanticsOfAnyHop()
    {
        var header = ForwardedHeaderParser.Parse(
            "for=\"not an IP\";host=\"bad / host\";proto=\"1:bad\",for=192.0.2.1;proto=https");
        header.Elements.Count.ShouldBe(2);
        header.Elements[0].For.ShouldBe("not an IP");
        header.Elements[0].Host.ShouldBe("bad / host");
        header.Elements[0].Proto.ShouldBe("1:bad");
        header.Elements[1].For.ShouldBe("192.0.2.1");
    }

    [Fact]
    public void HeaderAndElementCollectionsAreImmutableSnapshots()
    {
        var source = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["for"] = "a" };
        var element = new ForwardedElement(source, 0);
        var sourceElements = new List<ForwardedElement> { element };
        var header = new ForwardedHeader("for=a", sourceElements);
        source["for"] = "b";
        source["new"] = "value";
        sourceElements.Clear();

        header.Elements.Single().For.ShouldBe("a");
        element.Parameters.Count.ShouldBe(1);
        Should.Throw<NotSupportedException>(() =>
            ((IDictionary<string, string>)element.Parameters).Add("proto", "https"));
        Should.Throw<NotSupportedException>(() =>
            ((IDictionary<string, string>)element.Parameters)["for"] = "c");
        Should.Throw<NotSupportedException>(() => ((IList<ForwardedElement>)header.Elements).Clear());
    }

    [Fact]
    public void GeneratedEscapedValuesAndEmptySlotsRoundTripWithoutLosingBoundaries()
    {
        string[] values = ["", "plain", "a,b;c=d", "\"\\", "\t", "\u0080\u00ff", "[::1]:80"];
        string[] padding = ["", " ", "\t ", " \t"];
        foreach (var first in values)
        foreach (var second in values)
        foreach (var whitespace in padding)
        {
            var prefix = $"{whitespace};x={Quote(first)};;";
            var input = $"{prefix}{whitespace},{whitespace};X={Quote(second)};{whitespace}";
            var header = ForwardedHeaderParser.Parse(input);
            header.Elements.Count.ShouldBe(2);
            header.Elements[0].Parameters["x"].ShouldBe(first);
            header.Elements[1].Parameters["x"].ShouldBe(second);
            header.Value[..header.Elements[1].PrefixLength].ShouldBe(prefix + whitespace);
        }
    }

    [Fact]
    public void LongHeadersAndQuotedValuesAreParsedWithoutRecursion()
    {
        const int count = 5000;
        var joined = string.Join(",", Enumerable.Range(0, count).Select(index => $"for=_hop{index};x=\"a,b;c\""));
        var header = ForwardedHeaderParser.Parse(joined);
        header.Elements.Count.ShouldBe(count);
        for (var index = 0; index < count; index++)
        {
            header.Elements[index].For.ShouldBe($"_hop{index}");
        }

        var manyParameters = string.Join(";", Enumerable.Range(0, count).Select(index => $"x{index}=v"));
        ForwardedHeaderParser.Parse(manyParameters).Elements.Single().Parameters.Count.ShouldBe(count);

        var longText = new string('a', 100_000) + "\"\\,;";
        ForwardedHeaderParser.Parse($"x={Quote(longText)}").Elements.Single().Parameters["x"].ShouldBe(longText);
        ForwardedHeaderParser.Parse(new string(';', 100_000)).Elements.Single().Parameters.ShouldBeEmpty();
        ForwardedHeaderParser.Parse(new string(',', 100_000)).Elements.ShouldBeEmpty();
        AssertMalformed("x=\"" + new string('a', 100_000));
    }

    [Fact]
    public void DeterministicArbitraryInputNeverThrowsForMalformedSyntax()
    {
        var random = new Random(7239);
        const string alphabet = "for=byhostproto;,\"\\[] \t\r\n012.abc_\0\u007f\u0080\u00ff\u0100";
        for (var sample = 0; sample < 3000; sample++)
        {
            var characters = new char[random.Next(0, 129)];
            for (var index = 0; index < characters.Length; index++)
            {
                characters[index] = alphabet[random.Next(alphabet.Length)];
            }

            var input = new string(characters);
            var success = ForwardedHeaderParser.TryParse(input, out var header, out var error);
            if (success)
            {
                header.ShouldNotBeNull();
                header.Value.ShouldBe(input);
                error.ShouldBeNull();
            }
            else
            {
                header.ShouldBeNull();
                error.ShouldNotBeNull();
                error.Position.ShouldBeInRange(0, input.Length);
                error.Message.ShouldNotBeNullOrEmpty();
            }
        }
    }

    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static void AssertMalformed(string value)
    {
        ForwardedHeaderParser.TryParse(value, out var header, out var error).ShouldBeFalse();
        header.ShouldBeNull();
        error.ShouldNotBeNull();
        error.Position.ShouldBeInRange(0, value.Length);
        error.Message.ShouldNotBeNullOrEmpty();
    }
}
