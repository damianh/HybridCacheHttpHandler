// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Http;

namespace DamianH.Http.ForwardedHeaders;

internal static class ForwardedValueValidator
{
    internal static bool IsToken(string value) =>
        value.Length > 0 && value.All(c => char.IsAsciiLetterOrDigit(c) || "!#$%&'*+-.^_`|~".Contains(c));

    internal static bool IsScheme(string value) =>
        value.Length > 0 && char.IsAsciiLetter(value[0])
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '+' or '-' or '.');

    internal static bool IsHost(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        var portSeparator = value.IndexOf(':');
        if (value[0] == '[')
        {
            var closingBracket = value.IndexOf(']');
            if (closingBracket < 0
                || !ForwardedNodeIdentifier.TryParse(value[..(closingBracket + 1)], out var node)
                || node.Address is null)
            {
                return false;
            }

            portSeparator = closingBracket + 1;
            if (portSeparator == value.Length)
            {
                return true;
            }
            if (value[portSeparator] != ':')
            {
                return false;
            }
        }
        else
        {
            var host = portSeparator < 0 ? value.AsSpan() : value.AsSpan(0, portSeparator);
            if (host.IsEmpty)
            {
                return false;
            }
            foreach (var c in host)
            {
                if (!char.IsAsciiLetterOrDigit(c) && !"!$&'()-.~_".Contains(c))
                {
                    return false;
                }
            }
        }

        return portSeparator < 0 || IsPort(value.AsSpan(portSeparator + 1));
    }

    private static bool IsPort(ReadOnlySpan<char> value) =>
        !value.IsEmpty && value.IndexOfAnyExceptInRange('0', '9') < 0
        && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var port)
        && port <= IPEndPoint.MaxPort;

    internal static bool TryPathBase(string value, out PathString pathBase)
    {
        pathBase = default;
        if (value.Length == 0)
        {
            pathBase = PathString.Empty;
            return true;
        }
        if (value[0] != '/' || value.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == '%')
            {
                if (i + 2 >= value.Length || !char.IsAsciiHexDigit(value[i + 1]) || !char.IsAsciiHexDigit(value[i + 2]))
                {
                    return false;
                }
                var octet = int.Parse(value.AsSpan(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                if (octet < 32 || octet == 127 || octet == '\\')
                {
                    return false;
                }
                i += 2;
            }
            else if (!char.IsAsciiLetterOrDigit(c) && !"/-._~!$&'()*+,;=:@".Contains(c))
            {
                return false;
            }
        }

        pathBase = PathString.FromUriComponent(value);
        var decoded = pathBase.Value!;
        if (decoded.StartsWith("//", StringComparison.Ordinal) || decoded.Any(c => char.IsControl(c) || c == '\\'))
        {
            pathBase = default;
            return false;
        }
        return true;
    }
}
