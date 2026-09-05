using System.Diagnostics.CodeAnalysis;

namespace DamianH.Http.StructuredFieldValues;

/// <summary>
/// Extension methods for HttpResponseMessage to work with structured field values.
/// </summary>
public static class HttpResponseMessageExtensions
{
    extension(HttpResponseMessage response)
    {
        /// <summary>
        /// Tries to parse a response header as a strongly-typed structured field value.
        /// </summary>
        /// <typeparam name="T">The POCO type produced by the mapper.</typeparam>
        /// <param name="headerName">The header name.</param>
        /// <param name="mapper">The mapper used to parse the header value.</param>
        /// <param name="result">The parsed value if successful.</param>
        /// <returns>True if parsing succeeded, false otherwise.</returns>
        public bool TryGetHeader<T>(
            string headerName,
            StructuredFieldMapper<T> mapper,
            [NotNullWhen(true)] out T? result)
            where T : class, new()
        {
            result = default;

            if (!response.Headers.TryGetValues(headerName, out var values))
            {
                return false;
            }

            var headerValue = string.Join(", ", values);
            return mapper.TryParse(headerValue, out result);
        }
    }
}
