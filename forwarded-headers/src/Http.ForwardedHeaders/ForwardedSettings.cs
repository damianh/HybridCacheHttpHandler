// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace DamianH.Http.ForwardedHeaders;

internal sealed class ForwardedSettings
{
    private readonly IPAddress[] _proxies;
    private readonly IPNetwork[] _networks;
    private readonly StringSegment[] _hosts;
    private readonly bool _allowAllHosts;

    internal ForwardedSettings(ForwardedOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if ((options.Parameters & ~(ForwardedParameters.All | ForwardedParameters.PathBase)) != 0)
        {
            throw new ArgumentException("Unknown forwarding parameters.", nameof(options));
        }
        if (options.ForwardLimit < 0)
        {
            throw new ArgumentException("ForwardLimit must be nonnegative or null.", nameof(options));
        }
        if (!Enum.IsDefined(options.MalformedHeaderBehavior))
        {
            throw new ArgumentException("Unknown malformed-header behavior.", nameof(options));
        }

        string[] names = [options.HeaderName, options.OriginalForHeaderName, options.OriginalHostHeaderName,
            options.OriginalProtoHeaderName, options.OriginalPrefixHeaderName];
        if (names.Any(name => name is null || !ForwardedValueValidator.IsToken(name))
            || names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length)
        {
            throw new ArgumentException("Forwarding and original header names must be valid, distinct HTTP tokens.", nameof(options));
        }

        _proxies = options.KnownProxies.Select(address => ForwardedFeature.Copy(address)
            ?? throw new ArgumentException("KnownProxies must not contain null.", nameof(options))).ToArray();
        _networks = options.KnownIPNetworks.Select(network => new IPNetwork(
            ForwardedFeature.Copy(network.BaseAddress)!, network.PrefixLength)).ToArray();
        _hosts = options.AllowedHosts.Select(host =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(host);
            var asciiHost = new HostString(host).ToUriComponent();
            var hostToValidate = asciiHost.StartsWith("*.", StringComparison.Ordinal) ? asciiHost[2..] : asciiHost;
            if (asciiHost != "*" && (!ForwardedValueValidator.IsHost(hostToValidate)
                || new HostString(hostToValidate).Port.HasValue))
            {
                throw new ArgumentException("AllowedHosts entries must be hosts without ports, optionally with a subdomain wildcard.", nameof(options));
            }
            return new StringSegment(asciiHost);
        }).Distinct(StringSegmentComparer.OrdinalIgnoreCase).ToArray();
        _allowAllHosts = _hosts.Length == 0 || _hosts.Any(host => host.Value is "*" or "0.0.0.0" or "[::]");

        Parameters = options.Parameters;
        ForwardLimit = options.ForwardLimit;
        MalformedHeaderBehavior = options.MalformedHeaderBehavior;
        HeaderName = options.HeaderName;
        OriginalForHeaderName = options.OriginalForHeaderName;
        OriginalHostHeaderName = options.OriginalHostHeaderName;
        OriginalProtoHeaderName = options.OriginalProtoHeaderName;
        OriginalPrefixHeaderName = options.OriginalPrefixHeaderName;
    }

    internal ForwardedParameters Parameters { get; }
    internal int? ForwardLimit { get; }
    internal MalformedHeaderBehavior MalformedHeaderBehavior { get; }
    internal string HeaderName { get; }
    internal string OriginalForHeaderName { get; }
    internal string OriginalHostHeaderName { get; }
    internal string OriginalProtoHeaderName { get; }
    internal string OriginalPrefixHeaderName { get; }

    internal bool Has(ForwardedParameters parameter) => (Parameters & parameter) != 0;

    internal bool IsKnown(IPAddress? address)
    {
        if (address is null || (_proxies.Length == 0 && _networks.Length == 0))
        {
            return true;
        }
        if (address.IsIPv4MappedToIPv6 && IsKnown(address.MapToIPv4()))
        {
            return true;
        }
        return _proxies.Contains(address) || _networks.Any(network => network.Contains(address));
    }

    internal bool IsAllowedHost(string host) => _allowAllHosts || HostString.MatchesAny(host, _hosts);
}
