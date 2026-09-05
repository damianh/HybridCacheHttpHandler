# HTTP cache content-store abstractions

Provider-independent contracts for `DamianH.HttpHybridCacheHandler` and its content-store adapters.
No cloud SDK or HybridCache dependency is required to implement a store.

`IHttpCacheContentStore` provides complete streaming writes, independently owned read streams,
and single-key removal. `ILargeHttpCacheContentStore` identifies the optional external body store.
The handler retains responsibility for HTTP freshness, variant selection, and metadata invalidation.
Storage retention is separate from HTTP freshness.

Recognized SDK write failures are propagated as `HttpCacheContentStoreException`
(an `IOException`) with the original provider exception attached. Cancellation and
programming errors are not converted. This lets the handler log and abandon a failed
cache fill without depending on cloud SDK exception types or corrupting the origin
response. Missing read results alone return null; other read failures propagate.

This package is versioned independently using `cache-abstractions-v` tags.
