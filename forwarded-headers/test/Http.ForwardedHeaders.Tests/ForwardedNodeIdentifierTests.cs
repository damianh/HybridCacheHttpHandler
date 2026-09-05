// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Net.Sockets;
using Shouldly;

namespace DamianH.Http.ForwardedHeaders;

public sealed class ForwardedNodeIdentifierTests
{
    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("192.0.2.43")]
    [InlineData("127.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("1.20.100.254")]
    public void StrictIpv4AddressesAreAccepted(string value)
    {
        ForwardedNodeIdentifier.TryParse(value, out var node).ShouldBeTrue();
        node.ShouldNotBeNull();
        node.Address.ShouldBe(IPAddress.Parse(value));
        node.Address!.AddressFamily.ShouldBe(AddressFamily.InterNetwork);
        node.IsUnknown.ShouldBeFalse();
        node.ObfuscatedName.ShouldBeNull();
        node.Port.ShouldBeNull();
        node.ObfuscatedPort.ShouldBeNull();
    }

    [Theory]
    [InlineData("[::]")]
    [InlineData("[::1]")]
    [InlineData("[2001:db8:cafe::17]")]
    [InlineData("[2001:DB8:CAFE::17]")]
    [InlineData("[2001:db8:0:1:2:3:4:5]")]
    [InlineData("[ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff]")]
    [InlineData("[::ffff:192.0.2.43]")]
    [InlineData("[::192.0.2.43]")]
    [InlineData("[2001:db8:0:0:0:0:192.0.2.43]")]
    public void BracketedIpv6AddressesIncludingStrictEmbeddedIpv4AreAccepted(string value)
    {
        ForwardedNodeIdentifier.TryParse(value, out var node).ShouldBeTrue();
        node.ShouldNotBeNull();
        node.Address.ShouldBe(IPAddress.Parse(value[1..^1]));
        node.Address!.AddressFamily.ShouldBe(AddressFamily.InterNetworkV6);
        node.IsUnknown.ShouldBeFalse();
        node.ObfuscatedName.ShouldBeNull();
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("UNKNOWN")]
    [InlineData("UnKnOwN")]
    public void UnknownNodesAreCaseInsensitive(string value)
    {
        ForwardedNodeIdentifier.TryParse(value, out var node).ShouldBeTrue();
        node.ShouldNotBeNull();
        node.IsUnknown.ShouldBeTrue();
        node.Address.ShouldBeNull();
        node.ObfuscatedName.ShouldBeNull();
        node.Port.ShouldBeNull();
        node.ObfuscatedPort.ShouldBeNull();
    }

    [Theory]
    [InlineData("_gazonk")]
    [InlineData("_")]
    [InlineData("__")]
    [InlineData("_.")]
    [InlineData("_-")]
    [InlineData("_0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz._-")]
    [InlineData("_UNKNOWN")]
    public void ObfuscatedNodesRequireTheDefinedAlphabetAndOneCharacterAfterUnderscore(string value)
    {
        var success = ForwardedNodeIdentifier.TryParse(value, out var node);
        success.ShouldBe(value.Length > 1);
        if (success)
        {
            node.ShouldNotBeNull();
            node.ObfuscatedName.ShouldBe(value);
            node.Address.ShouldBeNull();
            node.IsUnknown.ShouldBeFalse();
            node.Port.ShouldBeNull();
            node.ObfuscatedPort.ShouldBeNull();
        }
        else
        {
            node.ShouldBeNull();
        }
    }

    [Theory]
    [InlineData("127")]
    [InlineData("127.1")]
    [InlineData("127.0.1")]
    [InlineData("2130706433")]
    [InlineData("0x7f000001")]
    [InlineData("0x7f.0.0.1")]
    [InlineData("0177.0.0.1")]
    [InlineData("192.168.001.1")]
    [InlineData("192.168.0.01")]
    [InlineData("00.0.0.0")]
    [InlineData("256.0.0.1")]
    [InlineData("1.2.3.999")]
    [InlineData("1.2.3.4.5")]
    [InlineData(".1.2.3")]
    [InlineData("1..2.3")]
    [InlineData("1.2.3.")]
    [InlineData("1.2.3.4.")]
    [InlineData("+1.2.3.4")]
    [InlineData("-1.2.3.4")]
    [InlineData("1.2.3.4/24")]
    [InlineData("1.2.3.4%0")]
    [InlineData("１２７.0.0.1")]
    [InlineData("[192.0.2.43]")]
    [InlineData("[127.1]")]
    public void NonRfcIpv4FormsAreRejected(string value)
    {
        AssertInvalid(value);
    }

    [Theory]
    [InlineData("::1")]
    [InlineData("2001:db8::1")]
    [InlineData("2001:db8::1:80")]
    [InlineData("::ffff:192.0.2.43")]
    [InlineData("[::1")]
    [InlineData("::1]")]
    [InlineData("[::1]]")]
    [InlineData("[[::1]]")]
    [InlineData("[]")]
    [InlineData("[:::1]")]
    [InlineData("[2001:db8::1::2]")]
    [InlineData("[1:2:3:4:5:6:7]")]
    [InlineData("[1:2:3:4:5:6:7:8:9]")]
    [InlineData("[12345::]")]
    [InlineData("[gggg::1]")]
    [InlineData("[fe80::1%eth0]")]
    [InlineData("[fe80::1%1]")]
    [InlineData("[fe80::1%0]")]
    [InlineData("[fe80::1%25eth0]")]
    [InlineData("[::1/128]")]
    [InlineData("[::ffff:127.1]")]
    [InlineData("[::ffff:192.168.001.1]")]
    [InlineData("[::ffff:192.168.0.01]")]
    [InlineData("[::ffff:0x7f.0.0.1]")]
    [InlineData("[::ffff:256.0.0.1]")]
    [InlineData("[::ffff:2130706433]")]
    [InlineData("[::1]junk")]
    [InlineData("[ ::1]")]
    [InlineData("[::1 ]")]
    public void InvalidIpv6BracketsZonesAndEmbeddedIpv4AreRejected(string value)
    {
        AssertInvalid(value);
    }

    [Fact]
    public void EveryNodeCategoryAcceptsNumericAndObfuscatedPorts()
    {
        string[] names = ["192.0.2.1", "[::1]", "[::ffff:192.0.2.1]", "unknown", "UNKNOWN", "_Proxy"];
        (string Text, int? Numeric, string? Obfuscated)[] ports =
        [
            ("0", 0, null), ("1", 1, null), ("80", 80, null), ("4711", 4711, null),
            ("65535", 65535, null), ("65536", 65536, null), ("99999", 99999, null),
            ("00000", 0, null), ("00080", 80, null), ("_secret", null, "_secret"),
            ("__", null, "__"), ("_.", null, "_."), ("_-", null, "_-"),
            ("_ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.-_", null,
                "_ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.-_")
        ];
        foreach (var name in names)
        foreach (var port in ports)
        {
            ForwardedNodeIdentifier.TryParse($"{name}:{port.Text}", out var node).ShouldBeTrue();
            node.ShouldNotBeNull();
            node.Port.ShouldBe(port.Numeric);
            node.ObfuscatedPort.ShouldBe(port.Obfuscated);
            node.IsUnknown.ShouldBe(name.Equals("unknown", StringComparison.OrdinalIgnoreCase));
            node.ObfuscatedName.ShouldBe(name[0] == '_' ? name : null);
            (node.Address is not null).ShouldBe(name[0] != '_' &&
                !name.Equals("unknown", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void InvalidPortFormsAreRejectedForEveryNodeCategory()
    {
        string[] names = ["192.0.2.1", "[::1]", "unknown", "_Proxy"];
        string[] ports =
        [
            "", "_", "100000", "000000", "-1", "+1", " 80", "80 ", "\t80", "80\t",
            "1.0", "1e3", "0x50", "８０", "١", "http", "a", "_a+b", "_a/b",
            "_a:b", "_é", "_a\r", "_a\n", "80:90", ":80", "[80]", "\"80\""
        ];
        foreach (var name in names)
        foreach (var port in ports)
        {
            AssertInvalid($"{name}:{port}");
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("hostname")]
    [InlineData("UnknownNode")]
    [InlineData("unknown.")]
    [InlineData("_é")]
    [InlineData("_a+b")]
    [InlineData("_a/b")]
    [InlineData("_a\\b")]
    [InlineData("_a!")]
    [InlineData("_a%")]
    [InlineData("_a=b")]
    [InlineData("_a b")]
    [InlineData("_a\tb")]
    [InlineData(" unknown")]
    [InlineData("unknown ")]
    [InlineData("192.0.2.1 ")]
    [InlineData("\t192.0.2.1")]
    [InlineData("\r192.0.2.1")]
    [InlineData("192.0.2.1\n")]
    [InlineData("\"192.0.2.1\"")]
    [InlineData("\"[::1]:80\"")]
    [InlineData("unknown:")]
    [InlineData(":80")]
    public void InvalidNamesWhitespaceAndStillQuotedValuesAreRejected(string value)
    {
        AssertInvalid(value);
    }

    [Fact]
    public void HeaderUnquotingAndNodeParsingComposeWithoutMixingTheirGrammars()
    {
        var element = ForwardedHeaderParser.Parse("for=\"[2001:db8:cafe::17]\\:4711\";by=\"_proxy:_port\"")
            .Elements.Single();
        ForwardedNodeIdentifier.TryParse(element.For!, out var client).ShouldBeTrue();
        client.ShouldNotBeNull();
        client.Address.ShouldBe(IPAddress.Parse("2001:db8:cafe::17"));
        client.Port.ShouldBe(4711);

        ForwardedNodeIdentifier.TryParse(element.By!, out var proxy).ShouldBeTrue();
        proxy.ShouldNotBeNull();
        proxy.ObfuscatedName.ShouldBe("_proxy");
        proxy.ObfuscatedPort.ShouldBe("_port");
    }

    [Fact]
    public void AddressExposureCannotMutateTheModelViaScopeId()
    {
        ForwardedNodeIdentifier.TryParse("[fe80::1]", out var node).ShouldBeTrue();
        node.ShouldNotBeNull();
        var address = node.Address;
        address.ShouldNotBeNull();
        address.ScopeId = 123;
        node.Address!.ScopeId.ShouldBe(0);
        node.Address.ShouldNotBeSameAs(address);
        node.Address.ShouldBe(IPAddress.Parse("fe80::1"));
    }

    [Fact]
    public void NullInputIsAProgrammerError()
    {
        Should.Throw<ArgumentNullException>(() => ForwardedNodeIdentifier.TryParse(null!, out _));
    }

    [Fact]
    public void GeneratedIpv4OctetsEnforceBoundsAndLeadingZeroRulesAtEveryPosition()
    {
        for (var position = 0; position < 4; position++)
        for (var number = 0; number <= 256; number++)
        {
            var octets = new[] { "192", "0", "2", "1" };
            octets[position] = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var value = string.Join(".", octets);
            ForwardedNodeIdentifier.TryParse(value, out var node).ShouldBe(number <= 255);
            if (number <= 255)
            {
                node.ShouldNotBeNull();
                node.Address!.ToString().ShouldBe(value);
            }

            octets[position] = "0" + octets[position];
            AssertInvalid(string.Join(".", octets));
        }
    }

    [Fact]
    public void ObfuscatedAlphabetIsAsciiOnlyInBothNameAndPort()
    {
        for (var code = 0; code <= 256; code++)
        {
            var character = (char)code;
            var allowed = character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9'
                or '.' or '_' or '-';
            ForwardedNodeIdentifier.TryParse($"_{character}", out _).ShouldBe(allowed);
            ForwardedNodeIdentifier.TryParse($"unknown:_{character}", out _).ShouldBe(allowed);
        }
    }

    [Fact]
    public void LongObfuscatedValuesAndMalformedNumbersAreHandledWithoutOverflow()
    {
        var name = "_" + new string('a', 100_000);
        var port = "_" + new string('0', 100_000);
        ForwardedNodeIdentifier.TryParse(name + ":" + port, out var node).ShouldBeTrue();
        node.ShouldNotBeNull();
        node.ObfuscatedName.ShouldBe(name);
        node.ObfuscatedPort.ShouldBe(port);

        AssertInvalid(new string('9', 100_000) + ".0.0.1");
        AssertInvalid("192.0.2.1:" + new string('9', 100_000));
        AssertInvalid("[" + new string(':', 100_000) + "]");
    }

    [Fact]
    public void DeterministicArbitraryNodeInputDoesNotThrow()
    {
        var random = new Random(7239);
        const string alphabet = "unknown_PROXY192.08abcdefABCDEF[]:%+-/ \t\r\n\0\u007f\u00ff";
        for (var sample = 0; sample < 3000; sample++)
        {
            var characters = new char[random.Next(0, 129)];
            for (var index = 0; index < characters.Length; index++)
            {
                characters[index] = alphabet[random.Next(alphabet.Length)];
            }

            var success = ForwardedNodeIdentifier.TryParse(new string(characters), out var node);
            if (success)
            {
                node.ShouldNotBeNull();
                ((node.Address is not null ? 1 : 0) + (node.IsUnknown ? 1 : 0) +
                    (node.ObfuscatedName is not null ? 1 : 0)).ShouldBe(1);
            }
            else
            {
                node.ShouldBeNull();
            }
        }
    }

    private static void AssertInvalid(string value)
    {
        ForwardedNodeIdentifier.TryParse(value, out var node).ShouldBeFalse();
        node.ShouldBeNull();
    }
}
