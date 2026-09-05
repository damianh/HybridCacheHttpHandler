// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Shouldly;

namespace DamianH.Http.ForwardedHeaders;

public class ForwardedMiddlewareTests
{
    [Fact]
    public async Task DefaultsAndMissingHeaderDoNotProcess()
    {
        var context = Context("for=192.0.2.1;proto=https");
        await Apply(context, new ForwardedOptions());
        context.Request.Scheme.ShouldBe("http");
        context.Connection.RemoteIpAddress.ShouldBe(IPAddress.Loopback);
        context.Features.Get<IForwardedFeature>().ShouldBeNull();

        context.Request.Headers.Clear();
        await Apply(context);
        context.Features.Get<IForwardedFeature>().ShouldBeNull();
    }

    [Fact]
    public async Task AppliesAllStandardValuesAndRecordsActualOriginals()
    {
        var context = Context("for=\"192.0.2.1:3210\";proto=https;host=\"example.com:8443\";by=_edge;extension=\"opaque,metadata\"");
        context.Request.Headers["X-Original-For"] = "spoof";
        context.Request.Headers["X-Original-Host"] = "spoof";

        await Apply(context);

        context.Connection.RemoteIpAddress.ShouldBe(IPAddress.Parse("192.0.2.1"));
        context.Connection.RemotePort.ShouldBe(3210);
        context.Request.Scheme.ShouldBe("https");
        context.Request.Host.ShouldBe(new HostString("example.com:8443"));
        context.Request.Headers.ContainsKey("Forwarded").ShouldBeFalse();
        context.Request.Headers["X-Original-For"].ToString().ShouldBe("127.0.0.1:5000");
        context.Request.Headers["X-Original-Host"].ToString().ShouldBe("backend.internal:8080");
        context.Request.Headers["X-Original-Proto"].ToString().ShouldBe("http");
        var feature = Feature(context);
        feature.OriginalRemoteIpAddress.ShouldBe(IPAddress.Loopback);
        feature.OriginalRemotePort.ShouldBe(5000);
        feature.OriginalHost.Value.ShouldBe("backend.internal:8080");
        feature.OriginalScheme.ShouldBe("http");
        feature.OriginalPathBase.Value.ShouldBe("/old");
        feature.StopReason.ShouldBe(ForwardedStopReason.Completed);
        feature.AcceptedHops.Single().Parameters["extension"].ShouldBe("opaque,metadata");
        feature.AcceptedHops.Single().By.ShouldBe("_edge");
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.8.9.10")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    public async Task LoopbackAndMappedAddressesAreTrusted(string peer)
    {
        var context = Context("for=192.0.2.1;proto=https", peer);
        await Apply(context);
        context.Request.Scheme.ShouldBe("https");
        context.Connection.RemoteIpAddress.ShouldBe(IPAddress.Parse("192.0.2.1"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UntrustedPeerIsIgnoredEvenWithMalformedHeader(bool strict)
    {
        var context = Context("for=\"broken", "192.0.2.10");
        var options = Options();
        options.MalformedHeaderBehavior = strict ? MalformedHeaderBehavior.Reject : MalformedHeaderBehavior.Ignore;
        await Apply(context, options);
        context.Response.StatusCode.ShouldBe(200);
        context.Request.Scheme.ShouldBe("http");
        context.Request.Headers["Forwarded"].ToString().ShouldBe("for=\"broken");
        Feature(context).StopReason.ShouldBe(ForwardedStopReason.UntrustedProxy);
    }

    [Fact]
    public async Task ExplicitProxyAndNetworkAreUsed()
    {
        var options = Options();
        options.ForwardLimit = null;
        options.KnownProxies.Add(IPAddress.Parse("10.1.1.1"));
        options.KnownIPNetworks.Add(IPNetwork.Parse("10.2.0.0/16"));
        var context = Context("for=192.0.2.1;proto=https,for=10.2.1.2", "10.1.1.1");
        await Apply(context, options);
        context.Connection.RemoteIpAddress.ShouldBe(IPAddress.Parse("192.0.2.1"));
        Feature(context).AcceptedHops.Count.ShouldBe(2);
    }

    [Fact]
    public async Task EmptyTrustListsDisableCheckingAsInAspNetCore()
    {
        var options = Options();
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        options.ForwardLimit = null;
        var context = Context("for=192.0.2.1;proto=https,for=10.1.1.1", "10.2.2.2");
        await Apply(context, options);
        context.Request.Scheme.ShouldBe("https");
        Feature(context).AcceptedHops.Count.ShouldBe(2);
    }

    [Fact]
    public async Task NullTransportPeerUsesFrameworkCompatibilityBehavior()
    {
        var context = Context("for=192.0.2.1;proto=https");
        context.Connection.RemoteIpAddress = null;
        await Apply(context);
        context.Request.Scheme.ShouldBe("https");
        context.Request.Headers.ContainsKey("X-Original-For").ShouldBeFalse();
        Feature(context).OriginalRemoteIpAddress.ShouldBeNull();
    }

    [Fact]
    public async Task NullTransportAndMissingIdentityCannotBridgeTrust()
    {
        var context = Context("for=192.0.2.1;proto=spoof,proto=https");
        context.Connection.RemoteIpAddress = null;
        var options = Options();
        options.ForwardLimit = null;
        await Apply(context, options);
        context.Request.Scheme.ShouldBe("https");
        context.Connection.RemoteIpAddress.ShouldBeNull();
        Feature(context).AcceptedHops.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(0, "http", 0)]
    [InlineData(1, "http", 1)]
    [InlineData(2, "https", 2)]
    [InlineData(null, "https", 2)]
    public async Task ForwardLimitIsSharedAcrossParameters(int? limit, string scheme, int hops)
    {
        var options = Options();
        options.ForwardLimit = limit;
        var context = Context("for=192.0.2.1;proto=https,for=127.0.0.2;proto=http");
        await Apply(context, options);
        context.Request.Scheme.ShouldBe(scheme);
        Feature(context).AcceptedHops.Count.ShouldBe(hops);
    }

    [Fact]
    public async Task ForDisabledStillUsesForToStopAtUntrustedPeer()
    {
        var options = Options();
        options.Parameters = ForwardedParameters.Proto;
        options.ForwardLimit = null;
        var context = Context("for=198.51.100.1;proto=spoof,for=192.0.2.1;proto=https,for=127.0.0.2;proto=http");
        await Apply(context, options);
        context.Request.Scheme.ShouldBe("https");
        context.Connection.RemoteIpAddress.ShouldBe(IPAddress.Loopback);
        context.Connection.RemotePort.ShouldBe(5000);
        Feature(context).AcceptedHops.Count.ShouldBe(2);
        Feature(context).StopReason.ShouldBe(ForwardedStopReason.UntrustedProxy);
        context.Request.Headers["Forwarded"].ToString().ShouldBe("for=198.51.100.1;proto=spoof");
    }

    [Theory]
    [InlineData("for=unknown;")]
    [InlineData("for=_hidden;")]
    [InlineData("for=\"unknown:1234\";")]
    [InlineData("for=\"unknown:99999\";")]
    [InlineData("for=\"_hidden:_port\";")]
    [InlineData("")]
    public async Task UnavailableIdentityAppliesUsableHopThenStops(string forPart)
    {
        var options = Options();
        options.ForwardLimit = null;
        options.Parameters |= ForwardedParameters.PathBase;
        var context = Context("for=192.0.2.1;proto=spoof," + forPart + "proto=https;host=example.com;pathbase=\"/public\"");
        await Apply(context, options);
        context.Request.Scheme.ShouldBe("https");
        context.Request.Host.Value.ShouldBe("example.com");
        context.Request.PathBase.Value.ShouldBe("/public");
        context.Connection.RemoteIpAddress.ShouldBe(IPAddress.Loopback);
        context.Connection.RemotePort.ShouldBe(5000);
        Feature(context).AcceptedHops.Count.ShouldBe(1);
        Feature(context).StopReason.ShouldBe(ForwardedStopReason.UnknownIdentity);
        context.Request.Headers["Forwarded"].ToString().ShouldBe("for=192.0.2.1;proto=spoof");
    }

    [Theory]
    [InlineData("extension=value")]
    [InlineData(";;")]
    public async Task EmptyOrExtensionOnlyElementCannotBridgeTrust(string element)
    {
        var options = Options();
        options.ForwardLimit = null;
        var context = Context("for=192.0.2.1;proto=spoof," + element + ",for=127.0.0.2;proto=https");
        await Apply(context, options);
        context.Request.Scheme.ShouldBe("https");
        Feature(context).StopReason.ShouldBe(ForwardedStopReason.UnknownIdentity);
    }

    [Theory]
    [InlineData("192.0.2.1", "192.0.2.1")]
    [InlineData("\"192.0.2.1:_private\"", "192.0.2.1")]
    [InlineData("\"[2001:db8::1]\"", "2001:db8::1")]
    public async Task AbsentAndObfuscatedPortsBecomeZero(string node, string address)
    {
        var context = Context($"for={node};proto=https");
        await Apply(context);
        context.Connection.RemoteIpAddress.ShouldBe(IPAddress.Parse(address));
        context.Connection.RemotePort.ShouldBe(0);
    }

    [Fact]
    public async Task MissingParametersRetainNearerValuesWithoutRealignment()
    {
        var options = Options();
        options.ForwardLimit = null;
        var context = Context("for=192.0.2.1;proto=https,for=127.0.0.2;host=near.example,for=127.0.0.3;proto=http");
        await Apply(context, options);
        context.Request.Host.Value.ShouldBe("near.example");
        context.Request.Scheme.ShouldBe("https");
        Feature(context).AcceptedHops.Select(hop => hop.For).ShouldBe(["127.0.0.3", "127.0.0.2", "192.0.2.1"]);
    }

    [Theory]
    [InlineData("for=\"broken")]
    [InlineData("for=192.0.2.1;For=192.0.2.2")]
    [InlineData("for=192.0.2.1;proto=https\r\n")]
    [InlineData("proto=https;bad")]
    public async Task StructuralFailureIgnoresWholeFieldByDefault(string header)
    {
        var context = Context(header);
        await Apply(context);
        AssertUnchanged(context, header);
        Feature(context).StopReason.ShouldBe(ForwardedStopReason.InvalidSyntax);
    }

    [Theory]
    [InlineData("for=999.0.0.1")]
    [InlineData("for=2130706433")]
    [InlineData("for=127.1")]
    [InlineData("for=\"192.0.2.1:65536\"")]
    [InlineData("for=192.0.2.1;proto=1https")]
    [InlineData("for=192.0.2.1;host=\"bad/host\"")]
    [InlineData("for=192.0.2.1;by=invalid")]
    public async Task InvalidHopKeepsValidNearerSuffixOnly(string invalidHop)
    {
        var options = Options();
        options.ForwardLimit = null;
        var context = Context(invalidHop + ",for=127.0.0.2;proto=https");
        await Apply(context, options);
        context.Request.Scheme.ShouldBe("https");
        context.Connection.RemoteIpAddress.ShouldBe(IPAddress.Parse("127.0.0.2"));
        context.Request.Headers["Forwarded"].ToString().ShouldBe(invalidHop);
        Feature(context).AcceptedHops.Count.ShouldBe(1);
        Feature(context).StopReason.ShouldBe(ForwardedStopReason.InvalidValue);
    }

    [Theory]
    [InlineData("for=\"broken")]
    [InlineData("for=invalid,for=127.0.0.2;proto=https")]
    [InlineData("for=192.0.2.1;host=bad.example,for=127.0.0.2;proto=https")]
    [InlineData("for=192.0.2.1;pathbase=\"relative\",for=127.0.0.2;proto=https")]
    public async Task StrictErrorsRejectWithoutPartialMutationsOrNextDelegate(string header)
    {
        var options = Options();
        options.Parameters |= ForwardedParameters.PathBase;
        options.ForwardLimit = null;
        options.MalformedHeaderBehavior = MalformedHeaderBehavior.Reject;
        options.AllowedHosts.Add("example.com");
        var context = Context(header);
        var nextCalls = 0;
        var middleware = Middleware(options, _ => { nextCalls++; return Task.CompletedTask; });
        await middleware.InvokeAsync(context);
        await middleware.InvokeAsync(context);

        context.Response.StatusCode.ShouldBe(400);
        nextCalls.ShouldBe(0);
        AssertUnchanged(context, header);
        context.Response.ContentLength.ShouldBeNull();
        Feature(context).Rejected.ShouldBeTrue();
        Feature(context).AcceptedHops.ShouldBeEmpty();
    }

    [Fact]
    public async Task StrictModeDoesNotValidateSemanticValuesBeyondBoundary()
    {
        var options = Options();
        options.ForwardLimit = null;
        options.MalformedHeaderBehavior = MalformedHeaderBehavior.Reject;
        var context = Context("for=invalid;proto=1bad,for=192.0.2.1;proto=https");
        await Apply(context, options);
        context.Response.StatusCode.ShouldBe(200);
        context.Request.Scheme.ShouldBe("https");
        Feature(context).StopReason.ShouldBe(ForwardedStopReason.UntrustedProxy);

        options.ForwardLimit = 1;
        context = Context("for=invalid;proto=1bad,for=127.0.0.2;proto=https");
        await Apply(context, options);
        context.Response.StatusCode.ShouldBe(200);
        Feature(context).StopReason.ShouldBe(ForwardedStopReason.ForwardLimit);
    }

    [Theory]
    [InlineData("example.com", "EXAMPLE.com:443", true)]
    [InlineData("*.example.com", "a.example.com", true)]
    [InlineData("*.example.com", "example.com", false)]
    [InlineData("example.com", "evil.example.com", false)]
    [InlineData("b\u00fccher.example", "xn--bcher-kva.example:443", true)]
    [InlineData("[2001:db8::1]", "[2001:db8::1]:443", true)]
    [InlineData("*", "any.example", true)]
    [InlineData("0.0.0.0", "any.example", true)]
    [InlineData("[::]", "any.example", true)]
    public async Task HostAllowlistMatchesFrameworkConventions(string allowed, string host, bool accepted)
    {
        var options = Options();
        options.AllowedHosts.Add(allowed);
        var context = Context($"for=192.0.2.1;host=\"{host}\"");
        await Apply(context, options);
        if (accepted)
        {
            context.Request.Host.ToUriComponent().ShouldBe(host);
        }
        else
        {
            context.Request.Host.Value.ShouldBe("backend.internal:8080");
            Feature(context).StopReason.ShouldBe(ForwardedStopReason.DisallowedHost);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(":443")]
    [InlineData("host:")]
    [InlineData("host:-1")]
    [InlineData("host:65536")]
    [InlineData("host:99999999999999999999")]
    [InlineData("host/path")]
    [InlineData("user@host")]
    [InlineData("host?query")]
    [InlineData("host#fragment")]
    [InlineData("bad host")]
    [InlineData("[not-ipv6]")]
    [InlineData("[fe80::1%25eth0]")]
    [InlineData("[::1]junk")]
    public async Task InvalidHostsDoNotReachRequest(string host)
    {
        var context = Context($"for=192.0.2.1;host=\"{host}\"");
        await Apply(context);
        Feature(context).StopReason.ShouldBe(ForwardedStopReason.InvalidValue);
        context.Request.Host.Value.ShouldBe("backend.internal:8080");
    }

    [Fact]
    public async Task DisallowedHostIsNotSkippedToReachEarlierClaim()
    {
        var options = Options();
        options.AllowedHosts.Add("good.example");
        options.ForwardLimit = null;
        var context = Context("for=192.0.2.1;host=good.example,for=127.0.0.2;host=evil.example,for=127.0.0.3;proto=https");
        await Apply(context, options);
        context.Request.Host.Value.ShouldBe("backend.internal:8080");
        Feature(context).AcceptedHops.Count.ShouldBe(1);
        Feature(context).StopReason.ShouldBe(ForwardedStopReason.DisallowedHost);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("/", "/")]
    [InlineData("/public", "/public")]
    [InlineData("/public/", "/public/")]
    [InlineData("/a%20b", "/a b")]
    public async Task PathBaseIsExplicitReplacement(string input, string expected)
    {
        var context = Context($"for=192.0.2.1;pathbase=\"{input}\"");
        var options = Options();
        options.Parameters |= ForwardedParameters.PathBase;
        await Apply(context, options);
        context.Request.PathBase.Value.ShouldBe(expected);
        context.Request.Path.Value.ShouldBe("/resource");
        context.Request.Headers["X-Original-Prefix"].ToString().ShouldBe("/old");
    }

    [Theory]
    [InlineData("relative")]
    [InlineData("//example.com/path")]
    [InlineData("/a?query")]
    [InlineData("/a#fragment")]
    [InlineData("/a%")]
    [InlineData("/a%2")]
    [InlineData("/a%xy")]
    [InlineData("/a%0a")]
    [InlineData("/a%00")]
    [InlineData("/a%7f")]
    [InlineData("/a%5cb")]
    [InlineData("/a b")]
    public async Task InvalidPathBaseUsesConfiguredFailurePolicy(string path)
    {
        foreach (var behavior in new[] { MalformedHeaderBehavior.Ignore, MalformedHeaderBehavior.Reject })
        {
            var context = Context($"for=192.0.2.1;pathbase=\"{path}\"");
            var options = Options();
            options.Parameters |= ForwardedParameters.PathBase;
            options.MalformedHeaderBehavior = behavior;
            var middleware = Middleware(options);
            await middleware.InvokeAsync(context);
            context.Request.PathBase.Value.ShouldBe("/old");
            context.Request.Path.Value.ShouldBe("/resource");
            Feature(context).StopReason.ShouldBe(ForwardedStopReason.InvalidValue);
            context.Response.StatusCode.ShouldBe(behavior == MalformedHeaderBehavior.Reject ? 400 : 200);
        }
    }

    [Fact]
    public async Task DisabledParametersRemainMetadataWithoutSemanticValidation()
    {
        var options = Options();
        options.Parameters = ForwardedParameters.Proto;
        var context = Context("for=192.0.2.1;proto=https;host=\"bad/host\";by=invalid;pathbase=relative");
        await Apply(context, options);
        context.Request.Host.Value.ShouldBe("backend.internal:8080");
        context.Request.PathBase.Value.ShouldBe("/old");
        context.Request.Scheme.ShouldBe("https");
        Feature(context).AcceptedHops.Single().Parameters["pathbase"].ShouldBe("relative");
    }

    [Fact]
    public async Task ByIsMetadataAndNeverGrantsTrust()
    {
        var options = Options();
        options.Parameters = ForwardedParameters.By;
        options.ForwardLimit = null;
        var context = Context("for=192.0.2.1;proto=spoof,for=198.51.100.1;by=127.0.0.2");
        await Apply(context, options);
        context.Request.Scheme.ShouldBe("http");
        Feature(context).AcceptedHops.Single().By.ShouldBe("127.0.0.2");
        Feature(context).StopReason.ShouldBe(ForwardedStopReason.UntrustedProxy);
    }

    [Fact]
    public async Task ForOnlyDoesNotRewriteOtherProperties()
    {
        var options = Options();
        options.Parameters = ForwardedParameters.For;
        var context = Context("for=192.0.2.1;proto=https;host=example.com;pathbase=\"/new\"");
        await Apply(context, options);
        context.Connection.RemoteIpAddress.ShouldBe(IPAddress.Parse("192.0.2.1"));
        context.Request.Scheme.ShouldBe("http");
        context.Request.Host.Value.ShouldBe("backend.internal:8080");
        context.Request.PathBase.Value.ShouldBe("/old");
        context.Request.Headers.ContainsKey("X-Original-Host").ShouldBeFalse();
        context.Request.Headers.ContainsKey("X-Original-Proto").ShouldBeFalse();
    }

    [Fact]
    public async Task InvalidForStopsTrustTraversalEvenWhenForMutationIsDisabled()
    {
        var options = Options();
        options.Parameters = ForwardedParameters.Proto;
        options.ForwardLimit = null;
        var context = Context("for=192.0.2.1;proto=spoof,for=invalid;proto=https");
        await Apply(context, options);
        context.Request.Scheme.ShouldBe("http");
        Feature(context).AcceptedHops.ShouldBeEmpty();
        Feature(context).StopReason.ShouldBe(ForwardedStopReason.InvalidValue);
    }

    [Fact]
    public async Task GrammarValidPortDoesNotPreventAddressTrustWhenForMutationIsDisabled()
    {
        var options = Options();
        options.Parameters = ForwardedParameters.Proto;
        options.ForwardLimit = null;
        var context = Context("for=192.0.2.1;proto=https,for=\"127.0.0.2:99999\"");
        await Apply(context, options);
        context.Request.Scheme.ShouldBe("https");
        context.Connection.RemotePort.ShouldBe(5000);
        Feature(context).AcceptedHops.Count.ShouldBe(2);
    }

    [Fact]
    public async Task PathBaseDoesNotCreateAnOriginalForAnEmptyBase()
    {
        var context = Context("pathbase=\"/public\"");
        context.Request.PathBase = PathString.Empty;
        var options = Options();
        options.Parameters = ForwardedParameters.PathBase;
        await Apply(context, options);
        context.Request.PathBase.Value.ShouldBe("/public");
        context.Request.Headers.ContainsKey("X-Original-Prefix").ShouldBeFalse();
    }

    [Fact]
    public async Task RetainsUnconsumedPrefixAndOpaqueExtensionsAcrossFieldLines()
    {
        var context = Context("");
        context.Request.Headers["Forwarded"] = new StringValues([
            "for=192.0.2.1;extension=\"raw,;\\\"text\"",
            " for=127.0.0.2;proto=https"]);
        await Apply(context);
        context.Request.Headers["Forwarded"].ToString().ShouldBe("for=192.0.2.1;extension=\"raw,;\\\"text\"");
        context.Request.Scheme.ShouldBe("https");
    }

    [Fact]
    public async Task RepeatedProcessingCannotConsumeMoreHops()
    {
        var context = Context("for=192.0.2.1;proto=spoof,for=127.0.0.2;proto=https");
        var middleware = Middleware(Options());
        await middleware.InvokeAsync(context);
        var firstFeature = Feature(context);
        var firstHeader = context.Request.Headers["Forwarded"];
        await middleware.InvokeAsync(context);
        await Middleware(Options()).InvokeAsync(context);
        Feature(context).ShouldBeSameAs(firstFeature);
        context.Request.Headers["Forwarded"].ShouldBe(firstHeader);
        context.Request.Scheme.ShouldBe("https");
        context.Request.Headers["X-Original-Proto"].ToString().ShouldBe("http");
    }

    [Fact]
    public async Task LegacyClaimsAreNeitherMergedNorConsumed()
    {
        var context = Context("for=192.0.2.1;proto=https");
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.1";
        context.Request.Headers["X-Forwarded-Proto"] = "spoof";
        context.Request.Headers["X-Forwarded-Host"] = "evil.example";
        context.Request.Headers["X-Forwarded-Prefix"] = "/evil";
        await Apply(context);
        context.Request.Scheme.ShouldBe("https");
        context.Request.Host.Value.ShouldBe("backend.internal:8080");
        context.Request.PathBase.Value.ShouldBe("/old");
        context.Request.Headers["X-Forwarded-For"].ToString().ShouldBe("198.51.100.1");
        context.Request.Headers["X-Forwarded-Proto"].ToString().ShouldBe("spoof");

        var legacyOnly = Context("");
        legacyOnly.Request.Headers.Remove("Forwarded");
        legacyOnly.Request.Headers["X-Forwarded-Proto"] = "https";
        await Apply(legacyOnly);
        legacyOnly.Request.Scheme.ShouldBe("http");
    }

    [Fact]
    public async Task MalformedInputDiagnosticsDoNotLogHeaderValues()
    {
        var logger = new RecordingLogger();
        var context = Context("for=private-client-value;host=secret.example");
        await Middleware(Options(), logger: logger).InvokeAsync(context);
        logger.Messages.ShouldNotBeEmpty();
        string.Join(" ", logger.Messages).ShouldNotContain("private-client");
        string.Join(" ", logger.Messages).ShouldNotContain("secret.example");
    }

    [Fact]
    public async Task ArbitraryHeaderTextDoesNotThrow()
    {
        var random = new Random(7239);
        for (var iteration = 0; iteration < 300; iteration++)
        {
            var text = new string(Enumerable.Range(0, random.Next(1, 100))
                .Select(_ => (char)random.Next(0, 300)).ToArray());
            await Apply(Context(text));
        }
    }

    internal static DefaultHttpContext Context(string header, string peer = "127.0.0.1")
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
        context.Connection.RemotePort = 5000;
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("backend.internal:8080");
        context.Request.PathBase = "/old";
        context.Request.Path = "/resource";
        context.Request.Headers["Forwarded"] = header;
        return context;
    }

    internal static ForwardedOptions Options() => new() { Parameters = ForwardedParameters.All };

    internal static ForwardedMiddleware Middleware(ForwardedOptions options, RequestDelegate? next = null,
        ILogger<ForwardedMiddleware>? logger = null) =>
        new(next ?? (_ => Task.CompletedTask), Microsoft.Extensions.Options.Options.Create(options),
            logger ?? NullLogger<ForwardedMiddleware>.Instance);

    private static async Task Apply(HttpContext context, ForwardedOptions? options = null)
    {
        var called = false;
        await Middleware(options ?? Options(), _ => { called = true; return Task.CompletedTask; }).InvokeAsync(context);
        called.ShouldBeTrue();
    }

    private static IForwardedFeature Feature(HttpContext context) =>
        context.Features.Get<IForwardedFeature>().ShouldNotBeNull();

    private static void AssertUnchanged(HttpContext context, string header)
    {
        context.Request.Headers["Forwarded"].ToString().ShouldBe(header);
        context.Connection.RemoteIpAddress.ShouldBe(IPAddress.Loopback);
        context.Connection.RemotePort.ShouldBe(5000);
        context.Request.Scheme.ShouldBe("http");
        context.Request.Host.Value.ShouldBe("backend.internal:8080");
        context.Request.PathBase.Value.ShouldBe("/old");
        context.Request.Path.Value.ShouldBe("/resource");
        context.Request.Headers.Keys.ShouldNotContain(key => key.StartsWith("X-Original-", StringComparison.OrdinalIgnoreCase));
    }
}
