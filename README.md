# http-libs

[![CI](https://github.com/damianh/http-libs/actions/workflows/hybrid-cache-handler-ci.yml/badge.svg)](https://github.com/damianh/http-libs/actions)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![GitHub Stars](https://img.shields.io/github/stars/damianh/http-libs.svg)](https://github.com/damianh/http-libs/stargazers)

A collection of .NET libraries for HTTP caching, structured field values, and message signatures.

> [!NOTE]
> Projects are new and being dog-fooded. If you try any of them out feedback would be appreciated!

## Packages

| Package | Description | NuGet | Downloads |
|---------|-------------|-------|-----------|
| [DamianH.HttpHybridCacheHandler](hybrid-cache-handler/README.md) | RFC 9111 client-side HTTP caching handler for `HttpClient` | [![NuGet](https://img.shields.io/nuget/v/DamianH.HttpHybridCacheHandler.svg)](https://www.nuget.org/packages/DamianH.HttpHybridCacheHandler/) | [![Downloads](https://img.shields.io/nuget/dt/DamianH.HttpHybridCacheHandler.svg)](https://www.nuget.org/packages/DamianH.HttpHybridCacheHandler/) |
| [DamianH.FileDistributedCache](file-distributed-cache/README.md) | File-based `IDistributedCache` / `IBufferDistributedCache` for zero-infrastructure persistent caching | [![NuGet](https://img.shields.io/nuget/v/DamianH.FileDistributedCache.svg)](https://www.nuget.org/packages/DamianH.FileDistributedCache/) | [![Downloads](https://img.shields.io/nuget/dt/DamianH.FileDistributedCache.svg)](https://www.nuget.org/packages/DamianH.FileDistributedCache/) |
| [DamianH.Http.StructuredFieldValues](structured-field-values/README.md) | RFC 8941/9651 parser, serializer, and POCO mapper for HTTP Structured Field Values | [![NuGet](https://img.shields.io/nuget/v/DamianH.Http.StructuredFieldValues.svg)](https://www.nuget.org/packages/DamianH.Http.StructuredFieldValues/) | [![Downloads](https://img.shields.io/nuget/dt/DamianH.Http.StructuredFieldValues.svg)](https://www.nuget.org/packages/DamianH.Http.StructuredFieldValues/) |
| [DamianH.Http.HttpSignatures](signatures/README.md) | RFC 9421 HTTP Message Signatures for signing and verifying HTTP messages | [![NuGet](https://img.shields.io/nuget/v/DamianH.Http.HttpSignatures.svg)](https://www.nuget.org/packages/DamianH.Http.HttpSignatures/) | [![Downloads](https://img.shields.io/nuget/dt/DamianH.Http.HttpSignatures.svg)](https://www.nuget.org/packages/DamianH.Http.HttpSignatures/) |

## Repository Structure

The HTTP cache also has separately versioned [content-store packages](hybrid-cache-handler/README.md#optional-content-stores)
for Azure Blob Storage, Amazon S3, Google Cloud Storage, and local files, sharing a
provider-independent Abstractions package. Cloud adapters use official SDKs.

```
hybrid-cache-handler/         # RFC 9111 HTTP caching DelegatingHandler
file-distributed-cache/       # File-based IDistributedCache implementation
structured-field-values/      # RFC 8941/9651 structured field values
signatures/                   # RFC 9421 HTTP message signatures
```

## Building

Each lib has its own build script and solution filter:

```bash
dotnet run signatures/build.cs -- build   # or: structured-field-values, hybrid-cache-handler, file-distributed-cache
dotnet build http-lib.slnx                # everything
```

## Running Tests

```bash
dotnet run signatures/build.cs -- test    # per lib
dotnet test http-lib.slnx                 # everything
```

## License

MIT — see [LICENSE](LICENSE).

## Contributing

Bug reports should be accompanied by a reproducible test case in a pull request.
