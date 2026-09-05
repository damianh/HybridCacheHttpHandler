// Copyright Damian Hickey

using System.Collections.Concurrent;
using System.Net;

namespace Benchmarks;

/// <summary>
/// Fake inner handler that serves pre-allocated byte payloads keyed by the
/// "size" query parameter. Payload buffers are allocated once and shared, so
/// per-request allocations attributable to the fake are limited to the
/// HttpResponseMessage + ByteArrayContent envelope.
/// </summary>
internal sealed class SizedFakeHandler : HttpMessageHandler
{
    private static readonly ConcurrentDictionary<int, byte[]> Payloads = new();
    private int _requestCount;

    public int RequestCount => Volatile.Read(ref _requestCount);

    public void ResetCounter() => Volatile.Write(ref _requestCount, 0);

    public static byte[] GetPayload(int size) =>
        Payloads.GetOrAdd(size, static s =>
        {
            var buffer = new byte[s];
            buffer.AsSpan().Fill((byte)'x');
            return buffer;
        });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _requestCount);

        var size = ParseSize(request.RequestUri?.Query);

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(GetPayload(size)),
            Headers = { { "Cache-Control", "max-age=3600" } },
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");

        if (request.Headers.Contains("Accept-Language"))
        {
            response.Headers.Add("Vary", "Accept-Language");
        }

        return Task.FromResult(response);
    }

    private static int ParseSize(string? query)
    {
        if (query is null)
        {
            return 1024;
        }

        const string Marker = "size=";
        var start = query.IndexOf(Marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return 1024;
        }

        start += Marker.Length;
        var end = start;
        while (end < query.Length && char.IsAsciiDigit(query[end]))
        {
            end++;
        }

        return int.TryParse(query.AsSpan(start, end - start), out var size) ? size : 1024;
    }
}

internal static class HttpResponseMessageExtensions
{
    /// <summary>
    /// Drains the response body to Stream.Null so benchmarks measure the
    /// handler's cost without adding string conversion or a buffered read stream.
    /// </summary>
    public static async Task DrainAsync(this HttpResponseMessage response)
    {
        using (response)
        {
            response.EnsureSuccessStatusCode();
            await response.Content.CopyToAsync(Stream.Null);
        }
    }
}
