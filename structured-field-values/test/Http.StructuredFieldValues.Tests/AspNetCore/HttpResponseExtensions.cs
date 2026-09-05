using DamianH.Http.StructuredFieldValues;

// ReSharper disable once CheckNamespace
namespace Microsoft.AspNetCore.Http;

/// <summary>
/// Extension methods for HttpResponse to work with structured field values.
/// </summary>
public static class HttpResponseExtensions
{
    extension(HttpResponse response)
    {
        /// <summary>
        /// Sets a response header from a strongly-typed structured field value.
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
            response.Headers[headerName] = serialized;
        }
    }
}
