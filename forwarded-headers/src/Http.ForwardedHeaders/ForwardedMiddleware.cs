// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DamianH.Http.ForwardedHeaders;

/// <summary>Applies RFC 7239 Forwarded values supplied by configured trusted proxies.</summary>
public sealed class ForwardedMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ForwardedSettings _settings;
    private readonly ILogger<ForwardedMiddleware> _logger;

    /// <summary>Creates middleware and snapshots its validated configuration.</summary>
    public ForwardedMiddleware(RequestDelegate next, IOptions<ForwardedOptions> options, ILogger<ForwardedMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _next = next;
        _settings = new ForwardedSettings(options.Value);
        _logger = logger;
    }

    /// <summary>Applies forwarding once per request, or rejects malformed input according to configuration.</summary>
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Features.Get<IForwardedFeature>() is { } existing)
        {
            if (existing.Rejected)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return Task.CompletedTask;
            }
            return _next(context);
        }
        if (_settings.Parameters == ForwardedParameters.None
            || !context.Request.Headers.TryGetValue(_settings.HeaderName, out var values))
        {
            return _next(context);
        }

        var feature = new ForwardedFeature(context);
        context.Features.Set<IForwardedFeature>(feature);
        if (!_settings.IsKnown(context.Connection.RemoteIpAddress))
        {
            feature.StopReason = ForwardedStopReason.UntrustedProxy;
            LogBoundary(feature.StopReason);
            return _next(context);
        }
        if (_settings.ForwardLimit == 0)
        {
            feature.StopReason = ForwardedStopReason.ForwardLimit;
            LogBoundary(feature.StopReason);
            return _next(context);
        }
        if (!ForwardedHeaderParser.TryParse(values.AsEnumerable(), out var header, out var error))
        {
            feature.StopReason = ForwardedStopReason.InvalidSyntax;
            _logger.LogWarning(new EventId(1, "InvalidForwardedSyntax"),
                "Invalid Forwarded header syntax at position {Position}.", error.Position);
            return FinishFailure(context, feature);
        }

        var accepted = new List<ForwardedElement>();
        var currentPeer = context.Connection.RemoteIpAddress;
        IPEndPoint? remote = null;
        string? scheme = null;
        HostString? host = null;
        PathString? pathBase = null;
        var prefixLength = header.Value.Length;

        for (var i = header.Elements.Count - 1; i >= 0; i--)
        {
            if (accepted.Count == _settings.ForwardLimit)
            {
                feature.StopReason = ForwardedStopReason.ForwardLimit;
                break;
            }
            if (!_settings.IsKnown(currentPeer))
            {
                feature.StopReason = ForwardedStopReason.UntrustedProxy;
                break;
            }

            var element = header.Elements[i];
            var failure = Validate(element, out var node, out var hopPathBase);
            if (failure.HasValue)
            {
                feature.StopReason = failure.Value;
                _logger.LogWarning(new EventId(2, "InvalidForwardedValue"),
                    "Forwarded processing stopped at hop {Hop}: {Reason}.", accepted.Count + 1, failure.Value);
                if (_settings.MalformedHeaderBehavior == MalformedHeaderBehavior.Reject)
                {
                    return FinishFailure(context, feature);
                }
                break;
            }

            if (_settings.Has(ForwardedParameters.For) && node?.Address is { } address)
            {
                remote = new IPEndPoint(address, node.Port ?? 0);
            }
            if (_settings.Has(ForwardedParameters.Proto) && element.Proto is { } hopScheme)
            {
                scheme = hopScheme;
            }
            if (_settings.Has(ForwardedParameters.Host) && element.Host is { } hopHost)
            {
                host = HostString.FromUriComponent(hopHost);
            }
            if (hopPathBase.HasValue)
            {
                pathBase = hopPathBase;
            }
            accepted.Add(element);
            prefixLength = element.PrefixLength;
            currentPeer = node?.Address;
            if (currentPeer is null)
            {
                feature.StopReason = ForwardedStopReason.UnknownIdentity;
                break;
            }
        }

        if (accepted.Count > 0)
        {
            Commit(context, remote, scheme, host, pathBase, header.Value, prefixLength);
            feature.AcceptedHops = accepted.AsReadOnly();
        }
        LogBoundary(feature.StopReason);
        return _next(context);
    }

    private ForwardedStopReason? Validate(ForwardedElement element, out ForwardedNodeIdentifier? node, out PathString? pathBase)
    {
        node = null;
        pathBase = null;
        // The for identity is needed for trust traversal even when address rewriting is disabled.
        if (element.For is { } forValue && (!ForwardedNodeIdentifier.TryParse(forValue, out node)
            || (_settings.Has(ForwardedParameters.For) && node.Address is not null && node.Port > IPEndPoint.MaxPort)))
        {
            return ForwardedStopReason.InvalidValue;
        }
        if (_settings.Has(ForwardedParameters.By) && element.By is { } byValue
            && !ForwardedNodeIdentifier.TryParse(byValue, out _))
        {
            return ForwardedStopReason.InvalidValue;
        }
        if (_settings.Has(ForwardedParameters.Proto) && element.Proto is { } scheme && !ForwardedValueValidator.IsScheme(scheme))
        {
            return ForwardedStopReason.InvalidValue;
        }
        if (_settings.Has(ForwardedParameters.Host) && element.Host is { } host)
        {
            if (!ForwardedValueValidator.IsHost(host))
            {
                return ForwardedStopReason.InvalidValue;
            }
            if (!_settings.IsAllowedHost(host))
            {
                return ForwardedStopReason.DisallowedHost;
            }
        }
        if (_settings.Has(ForwardedParameters.PathBase) && element.Parameters.TryGetValue("pathbase", out var path))
        {
            if (!ForwardedValueValidator.TryPathBase(path, out var parsedPath))
            {
                return ForwardedStopReason.InvalidValue;
            }
            pathBase = parsedPath;
        }
        return null;
    }

    private Task FinishFailure(HttpContext context, ForwardedFeature feature)
    {
        if (_settings.MalformedHeaderBehavior == MalformedHeaderBehavior.Reject)
        {
            feature.Rejected = true;
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return Task.CompletedTask;
        }
        return _next(context);
    }

    private void Commit(HttpContext context, IPEndPoint? remote, string? scheme, HostString? host,
        PathString? pathBase, string originalHeader, int prefixLength)
    {
        var headers = context.Request.Headers;
        if (remote is not null)
        {
            if (context.Connection.RemoteIpAddress is { } originalIp)
            {
                headers[_settings.OriginalForHeaderName] = new IPEndPoint(originalIp, context.Connection.RemotePort).ToString();
            }
            context.Connection.RemoteIpAddress = remote.Address;
            context.Connection.RemotePort = remote.Port;
        }
        if (scheme is not null)
        {
            headers[_settings.OriginalProtoHeaderName] = context.Request.Scheme;
            context.Request.Scheme = scheme;
        }
        if (host.HasValue)
        {
            headers[_settings.OriginalHostHeaderName] = context.Request.Host.ToString();
            context.Request.Host = host.Value;
        }
        if (pathBase.HasValue)
        {
            if (context.Request.PathBase.HasValue)
            {
                headers[_settings.OriginalPrefixHeaderName] = context.Request.PathBase.ToString();
            }
            context.Request.PathBase = pathBase.Value;
        }
        if (prefixLength == 0)
        {
            headers.Remove(_settings.HeaderName);
        }
        else
        {
            headers[_settings.HeaderName] = originalHeader[..prefixLength];
        }
    }

    private void LogBoundary(ForwardedStopReason reason)
    {
        if (reason is ForwardedStopReason.UntrustedProxy or ForwardedStopReason.UnknownIdentity or ForwardedStopReason.ForwardLimit)
        {
            _logger.LogDebug(new EventId(3, "ForwardedBoundary"), "Forwarded processing stopped: {Reason}.", reason);
        }
    }
}
