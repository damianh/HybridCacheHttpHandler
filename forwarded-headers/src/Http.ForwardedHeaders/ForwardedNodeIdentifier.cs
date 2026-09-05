// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace DamianH.Http.ForwardedHeaders;

/// <summary>
/// An immutable RFC 7239 node identifier, with an optional numeric or obfuscated port.
/// </summary>
public sealed class ForwardedNodeIdentifier
{
    private readonly IPAddress? _address;

    private ForwardedNodeIdentifier(
        IPAddress? address, bool isUnknown, string? obfuscatedName, int? port, string? obfuscatedPort)
    {
        _address = address;
        IsUnknown = isUnknown;
        ObfuscatedName = obfuscatedName;
        Port = port;
        ObfuscatedPort = obfuscatedPort;
    }

    /// <summary>
    /// Gets a defensive copy of the concrete IPv4 or IPv6 address, or null for unknown and obfuscated nodes.
    /// Mutating the returned address does not modify this identifier.
    /// </summary>
    public IPAddress? Address => _address is null ? null : new IPAddress(_address.GetAddressBytes());

    /// <summary>Gets whether the node name is the case-insensitive token <c>unknown</c>.</summary>
    public bool IsUnknown { get; }

    /// <summary>Gets the obfuscated node name, including its leading underscore, or null.</summary>
    public string? ObfuscatedName { get; }

    /// <summary>
    /// Gets the numeric port, or null when absent or obfuscated.
    /// RFC syntax permits values up to 99999; consumers must separately check socket-port limits.
    /// </summary>
    public int? Port { get; }

    /// <summary>Gets the obfuscated port, including its leading underscore, or null.</summary>
    public string? ObfuscatedPort { get; }

    /// <summary>Attempts to parse an already-unquoted RFC 7239 node identifier.</summary>
    /// <param name="value">The node identifier without HTTP quoting or quoted-pair escapes.</param>
    /// <param name="node">The immutable identifier on success; otherwise null.</param>
    /// <returns>Whether the identifier conforms to the node grammar.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static bool TryParse(string value, [NotNullWhen(true)] out ForwardedNodeIdentifier? node)
    {
        ArgumentNullException.ThrowIfNull(value);
        node = null;
        if (value.Length == 0)
        {
            return false;
        }

        IPAddress? address = null;
        var unknown = false;
        string? obfuscatedName = null;
        int nodeEnd;
        if (value[0] == '[')
        {
            var closingBracket = value.IndexOf(']');
            if (closingBracket < 0)
            {
                return false;
            }

            var literal = value.AsSpan(1, closingBracket - 1);
            if (literal.Contains('%'))
            {
                return false;
            }

            if (literal.Contains('.') && !IsIpv4(literal[(literal.LastIndexOf(':') + 1)..]))
            {
                return false;
            }

            if (!IPAddress.TryParse(literal, out address) || address.AddressFamily != AddressFamily.InterNetworkV6)
            {
                return false;
            }

            nodeEnd = closingBracket + 1;
        }
        else
        {
            nodeEnd = value.IndexOf(':');
            if (nodeEnd < 0)
            {
                nodeEnd = value.Length;
            }

            var name = value.AsSpan(0, nodeEnd);
            if (name.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            {
                unknown = true;
            }
            else if (IsObfuscated(name))
            {
                obfuscatedName = name.ToString();
            }
            else if (!IsIpv4(name) || !IPAddress.TryParse(name, out address))
            {
                return false;
            }
        }

        int? port = null;
        string? obfuscatedPort = null;
        if (nodeEnd < value.Length)
        {
            if (value[nodeEnd] != ':')
            {
                return false;
            }

            var portText = value.AsSpan(nodeEnd + 1);
            if (IsObfuscated(portText))
            {
                obfuscatedPort = portText.ToString();
            }
            else
            {
                if (portText.Length is < 1 or > 5)
                {
                    return false;
                }

                var number = 0;
                foreach (var character in portText)
                {
                    if (character is < '0' or > '9')
                    {
                        return false;
                    }

                    number = (number * 10) + character - '0';
                }

                port = number;
            }
        }

        node = new ForwardedNodeIdentifier(address, unknown, obfuscatedName, port, obfuscatedPort);
        return true;
    }

    private static bool IsIpv4(ReadOnlySpan<char> value)
    {
        var position = 0;
        for (var octet = 0; octet < 4; octet++)
        {
            var start = position;
            var number = 0;
            while (position < value.Length && value[position] is >= '0' and <= '9')
            {
                number = (number * 10) + value[position++] - '0';
                if (number > 255 || position - start > 3)
                {
                    return false;
                }
            }

            if (position == start || (position - start > 1 && value[start] == '0'))
            {
                return false;
            }

            if (octet == 3)
            {
                return position == value.Length;
            }

            if (position == value.Length || value[position++] != '.')
            {
                return false;
            }
        }

        return false;
    }

    private static bool IsObfuscated(ReadOnlySpan<char> value)
    {
        if (value.Length < 2 || value[0] != '_')
        {
            return false;
        }

        foreach (var character in value[1..])
        {
            if (character is not (>= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9'
                or '.' or '_' or '-'))
            {
                return false;
            }
        }

        return true;
    }
}
