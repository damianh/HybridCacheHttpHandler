// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;

namespace DamianH.HttpHybridCacheHandler;

internal static class VaryMatcher
{
    public static CachedHttpMetadata? SelectVariant(CachedHttpEntry entry, HttpRequestMessage request)
    {
        foreach (var variant in entry.Variants)
        {
            if (Matches(variant, request))
            {
                return variant;
            }
        }

        return null;
    }

    public static string BuildVariantSignature(CachedHttpMetadata variant) =>
        BuildVariantSignature(variant.VaryHeaders, variant.VaryHeaderValues);

    public static string BuildVariantSignature(string[]? varyHeaders, Dictionary<string, string>? varyHeaderValues)
    {
        if (varyHeaders == null || varyHeaders.Length == 0)
        {
            return "<no-vary>";
        }

        var parts = varyHeaders
            .Where(static h => !string.IsNullOrWhiteSpace(h))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static h => h, StringComparer.OrdinalIgnoreCase)
            .Select(h =>
            {
                var value = string.Empty;
                if (varyHeaderValues != null && varyHeaderValues.TryGetValue(h, out var storedValue))
                {
                    value = storedValue;
                }

                return $"{h.ToLowerInvariant()}={CanonicalizeForSignature(h, value)}";
            });

        return string.Join("|", parts);
    }

    public static string NormalizeHeaderValue(IEnumerable<string> values)
    {
        var normalizedTokens = values
            .SelectMany(static value => value.Split(','))
            .Select(static token => token.Trim())
            .Where(static token => token.Length > 0);

        return string.Join(",", normalizedTokens);
    }

    private static bool Matches(CachedHttpMetadata variant, HttpRequestMessage request)
    {
        if (variant.VaryHeaders == null || variant.VaryHeaders.Length == 0)
        {
            return true;
        }

        if (variant.VaryHeaderValues == null)
        {
            return false;
        }

        foreach (var varyHeader in variant.VaryHeaders)
        {
            var storedValue = variant.VaryHeaderValues.TryGetValue(varyHeader, out var value)
                ? value
                : string.Empty;

            var requestValue = GetNormalizedRequestHeaderValue(request, varyHeader);
            if (varyHeader.Equals("Accept-Language", StringComparison.OrdinalIgnoreCase))
            {
                if (!AcceptLanguageMatches(storedValue, requestValue, variant))
                {
                    return false;
                }

                continue;
            }

            if (!string.Equals(storedValue, requestValue, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static string GetNormalizedRequestHeaderValue(HttpRequestMessage request, string headerName) =>
        request.Headers.TryGetValues(headerName, out var values)
            ? NormalizeHeaderValue(values)
            : string.Empty;

    private static bool AcceptLanguageMatches(
        string storedRequestValue,
        string presentedRequestValue,
        CachedHttpMetadata variant)
    {
        if (string.Equals(storedRequestValue, presentedRequestValue, StringComparison.Ordinal))
        {
            return true;
        }

        if (AreEquivalentAcceptLanguageValues(storedRequestValue, presentedRequestValue))
        {
            return true;
        }

        if (string.IsNullOrEmpty(presentedRequestValue))
        {
            return false;
        }

        if (!TryGetContentLanguages(variant, out var contentLanguages))
        {
            return false;
        }

        var presentedRanges = ParseLanguageRanges(presentedRequestValue);
        if (presentedRanges.Count == 0)
        {
            return false;
        }

        return contentLanguages.Any(language => IsLanguageAcceptable(language, presentedRanges));
    }

    private static bool AreEquivalentAcceptLanguageValues(string left, string right)
    {
        var leftMap = BuildLanguageWeightMap(left);
        var rightMap = BuildLanguageWeightMap(right);

        if (leftMap.Count != rightMap.Count)
        {
            return false;
        }

        foreach (var item in leftMap)
        {
            if (!rightMap.TryGetValue(item.Key, out var rightWeight))
            {
                return false;
            }

            if (Math.Abs(item.Value - rightWeight) > 0.0001d)
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<string, double> BuildLanguageWeightMap(string value)
    {
        var map = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var range in ParseLanguageRanges(value))
        {
            if (!map.TryGetValue(range.Range, out var existing) || range.Weight > existing)
            {
                map[range.Range] = range.Weight;
            }
        }

        return map;
    }

    private static List<LanguageRange> ParseLanguageRanges(string value)
    {
        var ranges = new List<LanguageRange>();
        if (string.IsNullOrWhiteSpace(value))
        {
            return ranges;
        }

        foreach (var rawItem in value.Split(','))
        {
            var item = rawItem.Trim();
            if (item.Length == 0)
            {
                continue;
            }

            var semicolonIndex = item.IndexOf(';');
            var language = semicolonIndex >= 0 ? item[..semicolonIndex] : item;
            language = language.Trim().ToLowerInvariant();
            if (language.Length == 0)
            {
                continue;
            }

            var weight = 1d;
            if (semicolonIndex >= 0)
            {
                var parameters = item[(semicolonIndex + 1)..].Split(';');
                foreach (var rawParameter in parameters)
                {
                    var parameter = rawParameter.Trim();
                    if (!parameter.StartsWith("q=", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var rawQ = parameter[2..];
                    if (double.TryParse(rawQ, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsedQ))
                    {
                        weight = Math.Max(0d, Math.Min(1d, parsedQ));
                    }

                    break;
                }
            }

            ranges.Add(new LanguageRange(language, weight));
        }

        return ranges;
    }

    private static bool TryGetContentLanguages(CachedHttpMetadata variant, out List<string> contentLanguages)
    {
        contentLanguages = [];

        if (TryGetHeaderValues(variant.Headers, "Content-Language", out var responseHeaderValues))
        {
            AppendTokens(contentLanguages, responseHeaderValues);
        }

        if (TryGetHeaderValues(variant.ContentHeaders, "Content-Language", out var contentHeaderValues))
        {
            AppendTokens(contentLanguages, contentHeaderValues);
        }

        return contentLanguages.Count > 0;
    }

    private static bool TryGetHeaderValues(
        Dictionary<string, string[]> headers,
        string headerName,
        out string[] values)
    {
        if (headers.TryGetValue(headerName, out var headerValues) && headerValues != null)
        {
            values = headerValues;
            return true;
        }

        foreach (var item in headers)
        {
            if (!item.Key.Equals(headerName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            values = item.Value;
            return true;
        }

        values = [];
        return false;
    }

    private static void AppendTokens(List<string> target, IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            foreach (var token in value.Split(','))
            {
                var normalized = token.Trim();
                if (normalized.Length > 0)
                {
                    target.Add(normalized);
                }
            }
        }
    }

    private static bool IsLanguageAcceptable(string contentLanguage, IReadOnlyList<LanguageRange> presentedRanges)
    {
        var normalizedContentLanguage = contentLanguage.Trim().ToLowerInvariant();
        if (normalizedContentLanguage.Length == 0)
        {
            return false;
        }

        var bestWeight = 0d;
        foreach (var range in presentedRanges)
        {
            if (!LanguageRangeMatches(range.Range, normalizedContentLanguage))
            {
                continue;
            }

            if (range.Weight > bestWeight)
            {
                bestWeight = range.Weight;
            }
        }

        return bestWeight > 0d;
    }

    private static bool LanguageRangeMatches(string range, string languageTag)
    {
        if (range == "*")
        {
            return true;
        }

        if (languageTag.Equals(range, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return languageTag.StartsWith(range + "-", StringComparison.OrdinalIgnoreCase);
    }

    private static string CanonicalizeForSignature(string headerName, string value) =>
        headerName.Equals("Accept-Language", StringComparison.OrdinalIgnoreCase)
            ? CanonicalizeAcceptLanguage(value)
            : value;

    private static string CanonicalizeAcceptLanguage(string value)
    {
        var map = BuildLanguageWeightMap(value);
        if (map.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(",",
            map.OrderBy(static item => item.Key, StringComparer.Ordinal)
                .Select(static item => $"{item.Key};q={item.Value:0.###}"));
    }

    private readonly record struct LanguageRange(string Range, double Weight);
}
