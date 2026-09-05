using System.Diagnostics.CodeAnalysis;
using DamianH.Http.StructuredFieldValues;

namespace DamianH.Http.StructuredFieldValues;

/// <summary>
/// Extension methods for HttpRequestMessage to work with structured field values.
/// </summary>
public static class HttpRequestMessageExtensions
{
    extension(HttpRequestMessage request)
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

            if (!request.Headers.TryGetValues(headerName, out var values))
            {
                return false;
            }

            var headerValue = string.Join(", ", values);
            return mapper.TryParse(headerValue, out value);
        }

        /// <summary>
        /// Sets a request header from a strongly-typed structured field value using a mapper.
        /// </summary>
        /// <typeparam name="T">The POCO type serialized by the mapper.</typeparam>
        /// <param name="headerName">The header name.</param>
        /// <param name="mapper">The mapper used to serialize the value.</param>
        /// <param name="value">The value to serialize and set.</param>
        public void SetHeader<T>(
            string headerName,
            StructuredFieldMapper<T> mapper,
            T value)
            where T : class, new()
        {
            var serialized = mapper.Serialize(value);
            request.Headers.TryAddWithoutValidation(headerName, serialized);
        }
    }
}
