# DamianH.Http.ForwardedHeaders

Standalone [RFC 7239](https://www.rfc-editor.org/rfc/rfc7239.html) `Forwarded`
header parsing and trusted-proxy middleware for ASP.NET Core on .NET 10.

ASP.NET Core's built-in `UseForwardedHeaders` reads `X-Forwarded-*`, not RFC 7239.
This library processes `Forwarded` directly, keeping each hop's parameters
together. It is **not** a legacy-header adapter and never translates, merges,
prefers, or falls back to `X-Forwarded-*`. RFC 7239 is not an RFC 8941 Structured
Field; this package does not depend on the StructuredFieldValues library.

## Installation

```shell
dotnet add package DamianH.Http.ForwardedHeaders
```

The parser and middleware ship in the same package, which has a
`Microsoft.AspNetCore.App` framework reference. Even a parser-only application
requires the ASP.NET Core runtime. There is no public serializer in v1.

## One local proxy: quick start

```csharp
using DamianH.Http.ForwardedHeaders;
using Microsoft.AspNetCore.Http.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddForwarded(options =>
{
    options.Parameters = ForwardedParameters.For | ForwardedParameters.Host
        | ForwardedParameters.Proto;
    options.ForwardLimit = 1;
    // Retain the defaults: KnownProxies = ::1, KnownIPNetworks = 127.0.0.0/8.
    options.AllowedHosts.Add("localhost");
});

var app = builder.Build();
app.UseForwarded();
app.UseRouting();
app.MapGet("/request", (HttpRequest request) => request.GetEncodedUrl());
app.Run();
```

Use this trust configuration only for a local proxy and control direct client
access. Register forwarding early, before routing, HTTPS redirection,
authentication, authorization, and anything that generates external URLs.
`AddForwarded` uses the options system and validates at startup; middleware
snapshots its configuration. Alternatively, use
`app.UseForwarded(new ForwardedOptions { Parameters = ForwardedParameters.Proto })`
with explicitly configured trust for your deployment. Do not register both forms.

### Defaults and independent opt-ins

| Option | Default / meaning |
|---|---|
| `Parameters` | `None`: no forwarding, even from a known proxy |
| `ForwardLimit` | `1` accepted hop; `0` disables consumption; `null` removes the count limit |
| `KnownProxies` | `IPAddress.IPv6Loopback` (`::1`) |
| `KnownIPNetworks` | `System.Net.IPNetwork.Parse("127.0.0.0/8")` |
| `AllowedHosts` | Empty: any syntactically valid forwarded host |
| `MalformedHeaderBehavior` | `Ignore` |
| `HeaderName` | `Forwarded` |
| `OriginalForHeaderName` | `X-Original-For` |
| `OriginalHostHeaderName` | `X-Original-Host` |
| `OriginalProtoHeaderName` | `X-Original-Proto` |
| `OriginalPrefixHeaderName` | `X-Original-Prefix` |

`For`, `Host`, and `Proto` independently update the effective remote endpoint,
request host, and scheme. `By` enables validation of the `by` node identifier
as metadata only: **it never establishes proxy trust**.
`All` combines these four standard flags, but **excludes `PathBase`**.
Header names must be distinct valid HTTP tokens.

### Explicit production proxies and multiple hops

For `client -> edge (10.0.0.10) -> ingress (10.0.0.20) -> app`:

```csharp
using System.Net;
using DamianH.Http.ForwardedHeaders;

builder.Services.AddForwarded(options =>
{
    options.Parameters = ForwardedParameters.For | ForwardedParameters.Host
        | ForwardedParameters.Proto;
    options.ForwardLimit = 2;
    options.KnownProxies.Clear();
    options.KnownProxies.Add(IPAddress.Parse("10.0.0.20"));
    options.KnownProxies.Add(IPAddress.Parse("10.0.0.10"));
    options.KnownIPNetworks.Clear();
    options.AllowedHosts.Add("app.example.com");
});
```

Replace these example addresses with the actual transport peers. For one remote
proxy, configure only that address and set `ForwardLimit = 1`. If addresses rotate,
`KnownIPNetworks.Add(IPNetwork.Parse("10.0.0.0/24"))` can trust a dedicated proxy
subnet; do not trust a broad client-accessible network merely for convenience.

Traversal is right-to-left (nearest hop first), with one shared hop limit.
The transport peer must be trusted before its claims are examined. At each
subsequent hop, the previous hop's concrete `for` address must identify a trusted
proxy. This trust traversal still happens when `For` rewriting is disabled.
IPv4-mapped IPv6 peers also match IPv4 proxy/network entries.

A missing, `unknown`, or obfuscated `for` accepts the current trusted hop's usable
enabled values, then stops traversal. It does not change the remote endpoint.
A concrete IP with an omitted or obfuscated port can advance trust; applying it
sets `RemotePort` to **0**, not the previous node's port. The effective
`Connection.RemoteIpAddress`/`RemotePort` after forwarding are not necessarily
the physical connection peer.

RFC node syntax permits numeric ports up to 99999. Rewriting a concrete remote
endpoint additionally requires a socket port in the range 0-65535; metadata-only
ports do not establish or prevent address-based proxy trust.

`AllowedHosts` entries omit ports. Matching is case-insensitive, ignores the
forwarded port, and uses ASP.NET Core `HostString` matching, including IDN
configuration and `*.example.com` subdomain wildcards (not the parent domain).
An empty list or the unrestricted wildcard entries `*`, `0.0.0.0`, and `[::]`
allow all valid hosts. Incoming hosts use a deliberately safe ASP.NET Core-like
ASCII host-authority validator, not every RFC URI host form; use punycode for
internationalized names. Bracketed IPv6 is supported. Malformed and out-of-range
ports are rejected. Host allowlisting applies only when `Host` is enabled and
is not a replacement for filtering ordinary incoming `Host` headers.

## Nonstandard `pathbase` extension

Explicitly add `ForwardedParameters.PathBase` to `Parameters` to interpret
`pathbase`; RFC 7239 itself does **not** define this parameter.

```http
Forwarded: for=192.0.2.60;host="app.example.com:8443";proto=https;pathbase="/gateway"
```

The value replaces `Request.PathBase` using ASP.NET Core `PathString` URI
conversion. It never appends to PathBase, strips a prefix from `Request.Path`,
or rewrites Path. The proxy must already forward the intended application path.
`pathbase=""` clears PathBase; a missing parameter leaves the staged value alone.

Use a rooted URI path and quote it as required by HTTP token syntax (a slash
requires quoting). Network-path references (`//host`), non-rooted values,
query/fragment suffixes, backslashes, invalid escapes, and controls are invalid.
The extension follows the same trust, hop-limit, and error policies as the
standard parameters. When disabled it is uninterpreted metadata. Neither
`X-Forwarded-Prefix` nor `X-Forwarded-PathBase` is read.

## Malformed input and trust boundaries

| Policy | Invalid whole-field syntax | Invalid/disallowed value in a considered hop |
|---|---|---|
| `Ignore` (default) | Continue with request properties and forwarding headers unchanged | Stop before that hop; apply any fully validated nearer hops |
| `Reject` | HTTP 400, no next middleware, no forwarding mutations | HTTP 400, no next middleware, no forwarding mutations, including staged nearer hops |

Enable strict handling with
`options.MalformedHeaderBehavior = MalformedHeaderBehavior.Reject;`.
Rejection does not echo the raw header in a diagnostic response body.
Malformed hops are never skipped in favor of more distant claims. A disallowed
host stops its whole hop, rather than falling back to a farther host.

Unknown extensions, missing optional values, unknown/obfuscated identities,
untrusted peers, and hop limits are not malformed input. Semantic values beyond
a trust or hop boundary are not validated. Whole-field syntax must still parse
before individual element boundaries can be consumed safely, so a syntax error
in a farther element can invalidate the field. Disabled parameter values are not
semantically validated, except `for`, which is needed for trust traversal.

## Consumption, originals, and diagnostics

Accepted elements are removed from the right of `Forwarded`; an unconsumed prefix
is retained without reordering parameters or discarding unknown extensions. The
header is removed when completely consumed. Enabled applied values preserve
their pre-forwarding originals in the configurable `X-Original-*` headers using
ASP.NET Core conventions. Inbound original-value headers are **not** trusted
provenance.

```csharp
var feature = context.Features.Get<IForwardedFeature>();
if (feature is not null)
{
    var physicalPeer = feature.OriginalRemoteIpAddress;
    var acceptedCount = feature.AcceptedHops.Count;
    var outcome = feature.StopReason;
    var rejected = feature.Rejected;
}
```

`IForwardedFeature` captures immutable original remote IP/port, scheme, host, and
PathBase independently of inbound headers. `AcceptedHops` is nearest-first and
contains only accepted elements (empty on rejection), **not** the entire raw
chain. Its unselected parameters remain metadata, not semantically validated
claims. The feature also marks processing so re-execution or repeated middleware
registration cannot consume another batch of hops. It is absent when processing
is disabled or the header is absent. Structured log events describe failures and
boundaries without logging full headers or identifying values by default.

## Deployment safety

- Proxies must overwrite untrusted incoming `Forwarded` or append correctly
  while preserving an enforced trust boundary. **A trusted proxy can pass through
  a client-spoofed header unchanged**; its configured IP alone cannot make that
  header authentic. Restrict direct access to the application.
- For ASP.NET Core compatibility, emptying **both** `KnownProxies` and
  `KnownIPNetworks` disables address trust checks: it means **trust all**, not
  trust nobody. Use only with an independently enforced boundary.
- A null transport `RemoteIpAddress` can allow the first hop to be considered for
  compatibility with servers that cannot supply it. This provides no
  authenticated proxy identity. Without a concrete `for`, traversal stops after
  that hop; protect this deployment boundary externally.
- **Do not combine `UseForwarded` and `UseForwardedHeaders` on the same request.**
  Check automatic IIS and environment-enabled integrations (including
  `ASPNETCORE_FORWARDEDHEADERS_ENABLED`) as well as explicit registrations.
  This library does not disable host integrations. If both formats are needed,
  select separate configured pipelines/trust boundaries, not a fallback chain.
- Forwarded values describe proxy claims, not authenticated users. Avoid exposing
  raw headers, original headers, or a full proxy chain in public diagnostics.

## Direct parser use

```csharp
using DamianH.Http.ForwardedHeaders;

var header = ForwardedHeaderParser.Parse(
    "for=192.0.2.60;proto=https;host=\"app.example.com:8443\"");
var first = header.Elements[0];
var host = first.Host;
var parameters = first.Parameters;

if (ForwardedHeaderParser.TryParse(
    new string?[] { "for=192.0.2.60", "for=10.0.0.10;proto=https" },
    out var parsed, out var error))
{
    var hopCount = parsed.Elements.Count;
}

if (ForwardedNodeIdentifier.TryParse("[2001:db8::1]:443", out var node))
{
    // A parsed node is not evidence that this address is a trusted proxy.
}
```

`Parse` and `TryParse` accept either one string or ordered `IEnumerable<string?>`
field values. `ForwardedHeader.Value` retains the combined field text and
`Elements` preserves wire order. Each immutable `ForwardedElement` has a
case-insensitive `Parameters` dictionary and `For`, `By`, `Host`, `Proto`
convenience properties. Parameter values are unquoted/unescaped; unknown
extensions are retained.

The parser handles quoted separators, quoted-pair escapes, and empty list or
semicolon slots; it rejects duplicate parameter names (including case variants
and extensions). Syntax failures report an error position/reason; use `TryParse`
for untrusted input without exceptions. Field parsing is not host/scheme
validation or a trust decision. The separate node parser distinguishes concrete
IP, unknown, and obfuscated identities and numeric/obfuscated ports; RFC-valid
numeric port syntax is not necessarily a usable socket port.

## Run the sample

The [ASP.NET Core sample](samples/AspNetCoreSample/Program.cs) enables
`For | Host | Proto | PathBase`, trusts the loopback defaults, accepts one hop,
and allowlists `localhost`. From the repository root:

```powershell
dotnet run --project forwarded-headers\samples\AspNetCoreSample --urls http://127.0.0.1:5080
```

In another PowerShell 7.3+ window (with standard native argument passing),
simulate a local proxy using `curl.exe`:

```powershell
curl.exe -H 'Forwarded: for=192.0.2.60;host="localhost:8443";proto=https;pathbase="/gateway"' http://127.0.0.1:5080/request
```

The response URL is `https://localhost:8443/gateway/request`, with effective
remote IP `192.0.2.60` and port `0`. The actual endpoint path remains `/request`.
The sample returns neither the raw header nor the proxy chain.

Build or test this product using the shared repository build helpers:

```powershell
dotnet run forwarded-headers\build.cs -- build
dotnet run forwarded-headers\build.cs -- test
```
