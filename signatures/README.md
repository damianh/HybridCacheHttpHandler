# DamianH.Http.HttpSignatures

RFC 9421 HTTP Message Signatures for signing and verifying HTTP messages.

> **Depends on** `DamianH.Http.StructuredFieldValues` (pulled in automatically as a transitive dependency).

## Table of Contents

- [Installation](#installation)
- [Quick Start](#quick-start)
- [Supported Algorithms](#supported-algorithms)
- [API Reference](#api-reference)
  - [HttpMessageSigner](#httpmessagesigner)
  - [HttpMessageVerifier](#httpmessageverifier)
  - [Credentials](#credentials)
  - [Verification Policy](#verification-policy)
  - [SignatureParameters](#signatureparameters)
  - [ComponentIdentifier](#componentidentifier)
  - [IHttpMessageContext](#ihttpmessagecontext)
  - [IStructuredFieldTypeResolver](#istructuredfieldtyperesolver)
  - [SignatureResult](#signatureresult)
  - [VerificationResult](#verificationresult)
- [Key Types](#key-types)
- [Runtime Credential Resolution](#runtime-credential-resolution)
- [Security Boundaries](#security-boundaries)

## Installation

```bash
dotnet add package DamianH.Http.HttpSignatures
```

Repository builds reference the local Structured Field Values project, so both
libraries use the same object model. NuGet packaging records that project's
computed version as a dependency. For coordinated releases, publish the matching
Structured Field Values package before HttpSignatures, from the same source
commit.

## Quick Start

The following example shows a complete sign-then-verify round-trip using HMAC-SHA256 (symmetric — the same key is used for both operations):

```csharp
using DamianH.Http.HttpSignatures;
using DamianH.Http.HttpSignatures.Algorithms;
using DamianH.Http.HttpSignatures.Keys;

// --- Signing ---

var signingKey = new HmacSharedKey("my-key-id", Encoding.UTF8.GetBytes("super-secret"));
var algorithm = new HmacSha256SignatureAlgorithm();
var signingCredentials = new SigningCredentials(signingKey, algorithm);
var signer = new HttpMessageSigner();

var parameters = new SignatureParameters([
    ComponentIdentifier.Method,
    ComponentIdentifier.Authority,
    ComponentIdentifier.Path,
    ComponentIdentifier.Field("content-type"),
])
{
    Created = DateTimeOffset.UtcNow,
    KeyId = signingKey.KeyId,
    Algorithm = algorithm.AlgorithmName,
};

// context adapts your HTTP message (see IHttpMessageContext)
SignatureResult result = signer.Sign("sig1", context, parameters, signingCredentials);

// Add the headers to the outgoing request
request.Headers.Add("Signature-Input", result.SignatureInputHeaderValue);
request.Headers.Add("Signature", result.SignatureHeaderValue);

// --- Verification ---

var verificationKey = signingKey.AsVerificationKey();
var verificationCredentials = new VerificationCredentials(verificationKey, algorithm);
var verifier = new HttpMessageVerifier();

VerificationResult verification = verifier.Verify("sig1", context, verificationCredentials);

if (!verification.IsValid)
{
    Console.WriteLine($"Signature invalid: {verification.ErrorMessage}");
}
```

## Supported Algorithms

| Class | Algorithm Name | RFC Section | Key Types | Status |
|-------|---------------|-------------|-----------|--------|
| `HmacSha256SignatureAlgorithm` | `hmac-sha256` | §3.3.3 | `HmacSharedKey` / `HmacSharedVerificationKey` | ✅ Supported |
| `EcdsaP256Sha256SignatureAlgorithm` | `ecdsa-p256-sha256` | §3.3.4 | `EcdsaSigningKey` / `EcdsaVerificationKey` | ✅ Supported |
| `EcdsaP384Sha384SignatureAlgorithm` | `ecdsa-p384-sha384` | §3.3.5 | `EcdsaSigningKey` / `EcdsaVerificationKey` | ✅ Supported |
| `RsaPssSha512SignatureAlgorithm` | `rsa-pss-sha512` | §3.3.1 | `RsaSigningKey` / `RsaVerificationKey` | ✅ Supported |
| `RsaPkcs1Sha256SignatureAlgorithm` | `rsa-v1_5-sha256` | §3.3.2 | `RsaSigningKey` / `RsaVerificationKey` | ✅ Supported |
| `Ed25519SignatureAlgorithm` | `ed25519` | §3.3.6 | `Ed25519SigningKey` / `Ed25519VerificationKey` | ⚠️ Stub — throws `PlatformNotSupportedException` (awaiting .NET runtime support) |

## API Reference

### HttpMessageSigner

Signs an HTTP message, producing `Signature-Input` and `Signature` header values.

```csharp
public sealed class HttpMessageSigner
{
    public SignatureResult Sign(
        string label,
        IHttpMessageContext context,
        SignatureParameters parameters,
        SigningCredentials credentials,
        IStructuredFieldTypeResolver? fieldTypeResolver = null);
}
```

`fieldTypeResolver` declares the Structured Field type of HTTP fields, and is required to resolve `sf` and `key` components (see [IStructuredFieldTypeResolver](#istructuredfieldtyperesolver)). When omitted, every field's type is treated as unknown, so `sf`/`key` components fail explicitly instead of guessing the type from the field's value.

### HttpMessageVerifier

Performs protocol and cryptographic verification. `IsValid` does not mean that
application age, replay, tag, or required-component policy has accepted the message.

```csharp
public sealed class HttpMessageVerifier
{
    public VerificationResult Verify(
        string label,
        IHttpMessageContext context,
        VerificationCredentials credentials,
        IStructuredFieldTypeResolver? fieldTypeResolver = null);

    public ValueTask<VerificationResult> VerifyAsync(
        string label,
        IHttpMessageContext context,
        IVerificationCredentialsResolver credentialsResolver,
        IStructuredFieldTypeResolver? fieldTypeResolver = null,
        CancellationToken cancellationToken = default);
}
```

Expected input failures return a `VerificationResult` with a machine-readable
`FailureCode`. Cancellation and resolver, cryptographic-provider, or storage
infrastructure failures propagate to the caller.

### Credentials

`SigningCredentials` and `VerificationCredentials` bind key material to exactly
one trusted algorithm and validate compatibility when constructed. A signed
`alg` parameter is optional; when present, it must match the trusted algorithm.
A signed `keyid`, when present, must match the trusted credential identity.

ECDSA credentials validate the actual P-256 or P-384 curve, and ECDSA
verification enforces the RFC fixed signature size.

### Verification Policy

Use `VerifyAndValidateAsync` when successful cryptographic verification must
also satisfy explicit application requirements:

```csharp
var policy = new VerificationPolicy
{
    RequiredComponents =
    [
        ComponentIdentifier.Method,
        ComponentIdentifier.Authority,
        ComponentIdentifier.Field("content-digest"),
    ],
    RequireCreated = true,
    MaximumAge = TimeSpan.FromMinutes(5),
    ValidateExpiration = true,
    RequiredTag = "my-app",
    TimeProvider = TimeProvider.System,
};

VerificationAcceptanceResult acceptance =
    await verifier.VerifyAndValidateAsync(
        "sig1", context, verificationCredentials, policy);

if (!acceptance.IsAccepted)
{
    Console.WriteLine(acceptance.ErrorMessage);
}
```

Replay protection is opt-in through `INonceStore`. Its `TryUseAsync` operation
must atomically claim a nonce across every server sharing a replay scope. A
separate cache read followed by a write is not sufficient. When a nonce store
is configured, policy must enforce a finite acceptance window using
`MaximumAge` or expiration; the claim is retained through that deadline,
including clock skew. Storage adapters, including any HybridCache adapter, are
application concerns and are not included in this package.

### SignatureParameters

Defines the covered components and metadata for a signature. Covered components determine which parts of the HTTP message are included in the signature base.

```csharp
var parameters = new SignatureParameters([
    ComponentIdentifier.Method,
    ComponentIdentifier.Authority,
    ComponentIdentifier.Path,
])
{
    Created  = DateTimeOffset.UtcNow,       // ;created=<unix timestamp>
    Expires  = DateTimeOffset.UtcNow.AddMinutes(5), // ;expires=<unix timestamp>
    KeyId    = "my-key-id",                 // ;keyid="..."
    Nonce    = Guid.NewGuid().ToString(),   // ;nonce="..."
    Algorithm = "hmac-sha256",              // ;alg="..."
    Tag      = "my-app",                    // ;tag="..."
};
```

All properties except `CoveredComponents` are optional. Metadata is signed, but
`Verify` does not impose application policy merely because metadata is present.
Configure a `VerificationPolicy` to enforce creation age, expiration, nonce,
tag, or required-component rules.

### ComponentIdentifier

Identifies a component of the HTTP message to include in the signature base.

**Derived components** (start with `@`):

| Static property/method | Component name | Applies to |
|------------------------|---------------|------------|
| `ComponentIdentifier.Method` | `@method` | Request |
| `ComponentIdentifier.Authority` | `@authority` | Request |
| `ComponentIdentifier.Scheme` | `@scheme` | Request |
| `ComponentIdentifier.Path` | `@path` | Request |
| `ComponentIdentifier.Query` | `@query` | Request |
| `ComponentIdentifier.TargetUri` | `@target-uri` | Request |
| `ComponentIdentifier.RequestTarget` | `@request-target` | Request |
| `ComponentIdentifier.Status` | `@status` | Response |
| `ComponentIdentifier.QueryParam("name")` | `@query-param;name="..."` | Request |

**HTTP field components:**

| Factory method | Description |
|----------------|-------------|
| `ComponentIdentifier.Field("content-type")` | Raw header field value |
| `ComponentIdentifier.FieldSf("content-digest")` | Strict SF-serialized header value |
| `ComponentIdentifier.FieldKey("priority", "u")` | Specific key from an SF Dictionary header |
| `ComponentIdentifier.FieldBs("signature")` | Binary-wrapped header field |

### IHttpMessageContext

Adapts a concrete HTTP message to the interface required by `HttpMessageSigner` and `HttpMessageVerifier`. You implement this for your specific HTTP framework.

```csharp
public interface IHttpMessageContext
{
    bool IsRequest { get; }
    string? Method { get; }
    string? Scheme { get; }
    string? Authority { get; }
    string? Path { get; }
    string? Query { get; }
    string? TargetUri { get; }
    string? RequestTarget { get; }
    int? StatusCode { get; }
    IReadOnlyList<string> GetHeaderValues(string fieldName);
    IReadOnlyList<string> GetTrailerValues(string fieldName);
    IHttpMessageContext? AssociatedRequest { get; }
}
```

Implementations provide only the raw, uncombined field values, in field-line order, for headers (`GetHeaderValues`) and trailers (`GetTrailerValues`); a missing trailer must never fall back to a header of the same name, and the two sections are never combined together (RFC 9421 §2.1.4). The `HttpMessageContextExtensions.GetHeaderValue`/`GetTrailerValue` extension methods build the ordinary combined, canonicalized value (trimming, obsolete line-fold unwrapping, and comma-space combination per RFC 9110 §5.2) on top of these raw values, so most callers never need to implement combination themselves.

### IStructuredFieldTypeResolver

Declares the Structured Field Values (RFC 8941/9651) type of an HTTP field, required to resolve `sf` and `key` components deterministically instead of guessing the type by trying each parser in turn.

```csharp
public enum StructuredFieldValueKind { Unknown, Item, List, Dictionary }

public interface IStructuredFieldTypeResolver
{
    StructuredFieldValueKind ResolveType(bool isRequest, string fieldName);
}
```

A ready-to-use `DictionaryStructuredFieldTypeResolver` is provided, backed by a case-insensitive `IReadOnlyDictionary<string, StructuredFieldValueKind>` map of field name to declared type. Pass an instance to `HttpMessageSigner.Sign` / `SignatureBaseBuilder.Build` / `SignatureBaseBuilder.BuildString` via the optional `fieldTypeResolver` parameter.

### SignatureResult

Returned by `HttpMessageSigner.Sign`. Contains the values to set on the outgoing HTTP message headers.

| Property | Type | Description |
|----------|------|-------------|
| `Label` | `string` | The signature label (e.g., `"sig1"`) |
| `SignatureInputHeaderValue` | `string` | The value to add to the `Signature-Input` header |
| `SignatureHeaderValue` | `string` | The value to add to the `Signature` header |
| `SignatureBytes` | `ReadOnlySpan<byte>` | The raw signature bytes (defensively copied on construction; call `.ToArray()` for an owned copy) |

### VerificationResult

Returned by `HttpMessageVerifier.Verify` / `VerifyAsync`.

| Property | Type | Description |
|----------|------|-------------|
| `IsValid` | `bool` | `true` if the signature was successfully verified |
| `Parameters` | `SignatureParameters?` | The parsed signature parameters if available |
| `ErrorMessage` | `string?` | Description of failure when `IsValid` is `false` |

## Key Types

### Signing Keys

| Class | Constructor | Algorithm |
|-------|-------------|-----------|
| `HmacSharedKey` | `(string keyId, byte[] keyBytes)` | `hmac-sha256` |
| `EcdsaSigningKey` | `(string keyId, ECDsa ecdsa)` | `ecdsa-p256-sha256`, `ecdsa-p384-sha384` |
| `RsaSigningKey` | `(string keyId, RSA rsa)` | `rsa-pss-sha512`, `rsa-v1_5-sha256` |
| `Ed25519SigningKey` | `(string keyId, byte[] privateKeyBytes)` | `ed25519` ⚠️ stub |

### Verification Keys

| Class | Constructor | Notes |
|-------|-------------|-------|
| `HmacSharedVerificationKey` | `(string keyId, byte[] keyBytes)` | Obtain via `HmacSharedKey.AsVerificationKey()` |
| `EcdsaVerificationKey` | `(string keyId, ECDsa ecdsa)` | Public key sufficient |
| `RsaVerificationKey` | `(string keyId, RSA rsa)` | Public key sufficient |
| `Ed25519VerificationKey` | `(string keyId, byte[] publicKeyBytes)` | `ed25519` ⚠️ stub |

All key types carry a `KeyId`. Cryptographic objects remain caller-owned and
are never disposed by this library.

## Runtime Credential Resolution

For server-side verification, resolve a trusted key and its one allowed
algorithm together:

```csharp
public sealed class MyCredentialsResolver : IVerificationCredentialsResolver
{
    public async ValueTask<VerificationCredentials?> ResolveAsync(
        string keyId,
        CancellationToken cancellationToken = default)
    {
        VerificationKey? key = await _store.FindAsync(keyId, cancellationToken);
        return key is null
            ? null
            : new VerificationCredentials(key, new HmacSha256SignatureAlgorithm());
    }
}

var verifier = new HttpMessageVerifier();
var result = await verifier.VerifyAsync(
    "sig1", context, new MyCredentialsResolver());
```

`keyid` is required for runtime resolution. The incoming `alg` value does not
select an algorithm; it is checked for agreement with the trusted credential
when present. The message and signature base are snapshotted before the
resolver is awaited.

## Security Boundaries

- Signing a `Content-Digest` field protects that field's value; it does not
  compare the digest to the message body. Applications must perform body-digest
  verification separately.
- Signature verification and policy acceptance do not replace authorization.
- HTTP framework adapters must provide complete request/response context,
  including trailers, before signing or verification.
- This package defines the atomic nonce-store contract but does not provide a
  production distributed implementation.
