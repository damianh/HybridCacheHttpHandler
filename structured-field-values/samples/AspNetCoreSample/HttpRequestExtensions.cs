using System.Diagnostics.CodeAnalysis;
using DamianH.Http.StructuredFieldValues;

// ReSharper disable once CheckNamespace
namespace Microsoft.AspNetCore.Http;

/// <summary>
/// Extension methods for HttpRequest to work with structured field values.
/// </summary>
public static class HttpRequestExtensions
{
    extension(HttpRequest request)
    {
        /// <summary>
        /// Tries to parse a request header as a strongly-typed structured field value using a mapper.
        /// </summary>
        /// <typeparam name="T">The POCO type produced by the mapper.</typeparam>
        /// <param name="headerName">The header name.</param>
        /// <param name="mapper">The mapper used to parse the header value.</param>
        /// <param name="value">The parsed value if successful.</param>
        /// <returns>True if parsing succeeded, false otherwise.</returns>
        public bool TryGetHeader<T>(
            string headerName,
            StructuredFieldMapper<T> mapper,
            [NotNullWhen(true)] out T? value)
            where T : class, new()
        {
            value = default;

            if (!request.Headers.TryGetValue(headerName, out var values) || values.Count == 0)
            {
                return false;
            }

            var headerValue = values.ToString();
            return mapper.TryParse(headerValue, out value);
        }
    }
}
