// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.RegularExpressions;

namespace DamianH.HttpHybridCacheHandler;

/// <summary>
/// Source-generated regexes for cache-control parsing.
/// </summary>
internal static partial class CacheControlRegexes
{
    [GeneratedRegex(@"stale-while-revalidate=(\d+)", RegexOptions.IgnoreCase)]
    internal static partial Regex StaleWhileRevalidate();

    [GeneratedRegex(@"stale-if-error=(\d+)", RegexOptions.IgnoreCase)]
    internal static partial Regex StaleIfError();

    [GeneratedRegex(@"(?:^|,)\s*no-cache\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase)]
    internal static partial Regex QualifiedNoCache();

    [GeneratedRegex(@"(?:^|,)\s*no-cache\s*(?:,|$)", RegexOptions.IgnoreCase)]
    internal static partial Regex UnqualifiedNoCache();

    [GeneratedRegex(@"(?:^|,)\s*must-understand\s*(?:,|$)", RegexOptions.IgnoreCase)]
    internal static partial Regex MustUnderstand();

    [GeneratedRegex(@"(?:^|,)\s*max-age\s*=\s*(\d+)", RegexOptions.IgnoreCase)]
    internal static partial Regex MaxAge();

    [GeneratedRegex(@"(?:^|,)\s*s-maxage\s*=\s*(\d+)", RegexOptions.IgnoreCase)]
    internal static partial Regex SharedMaxAge();
}
