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
  - [SignatureParameters](#signatureparameters)
  - [ComponentIdentifier](#componentidentifier)
  - [IHttpMessageContext](#ihttpmessagecontext)
  - [SignatureResult](#signatureresult)
  - [VerificationResult](#verificationresult)
- [Key Types](#key-types)
- [Runtime Resolution](#runtime-resolution)

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
SignatureResult result = signer.Sign("sig1", context, parameters, signingKey, algorithm);

// Add the headers to the outgoing request
request.Headers.Add("Signature-Input", result.SignatureInputHeaderValue);
request.Headers.Add("Signature", result.SignatureHeaderValue);

// --- Verification ---

var verificationKey = signingKey.AsVerificationKey();
var verifier = new HttpMessageVerifier();

VerificationResult verification = verifier.Verify("sig1", context, verificationKey, algorithm);

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
        SigningKey key,
        ISignatureAlgorithm algorithm);
}
```

### HttpMessageVerifier

Verifies an HTTP message signature, either with explicit key/algorithm or via runtime resolution.

```csharp
public sealed class HttpMessageVerifier
{
    // Explicit key and algorithm
    public VerificationResult Verify(
        string label,
        IHttpMessageContext context,
        VerificationKey key,
        ISignatureAlgorithm algorithm);

    // Runtime key and algorithm resolution
    public Task<VerificationResult> VerifyAsync(
        string label,
        IHttpMessageContext context,
        IKeyResolver keyResolver,
        ISignatureAlgorithmRegistry algorithmRegistry,
        CancellationToken cancellationToken = default);
}
```

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

All properties except `CoveredComponents` are optional. Omitting `Created`/`Expires` means no time-bound validation.

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
    string? GetHeaderValue(string fieldName);
    IReadOnlyList<string> GetHeaderValues(string fieldName);
    IHttpMessageContext? AssociatedRequest { get; }
}
```

### SignatureResult

Returned by `HttpMessageSigner.Sign`. Contains the values to set on the outgoing HTTP message headers.

| Property | Type | Description |
|----------|------|-------------|
| `Label` | `string` | The signature label (e.g., `"sig1"`) |
| `SignatureInputHeaderValue` | `string` | The value to add to the `Signature-Input` header |
| `SignatureHeaderValue` | `string` | The value to add to the `Signature` header |
| `SignatureBytes` | `byte[]` | The raw signature bytes |

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
| `EcdsaSigningKey` | `(string keyId, ECDsa ecdsa, string? algorithmHint = null)` | `ecdsa-p256-sha256`, `ecdsa-p384-sha384` |
| `RsaSigningKey` | `(string keyId, RSA rsa, string? algorithmHint = null)` | `rsa-pss-sha512`, `rsa-v1_5-sha256` |
| `Ed25519SigningKey` | `(string keyId, byte[] privateKeyBytes)` | `ed25519` ⚠️ stub |

### Verification Keys

| Class | Constructor | Notes |
|-------|-------------|-------|
| `HmacSharedVerificationKey` | `(string keyId, byte[] keyBytes)` | Obtain via `HmacSharedKey.AsVerificationKey()` |
| `EcdsaVerificationKey` | `(string keyId, ECDsa ecdsa, string? algorithmHint = null)` | Public key sufficient |
| `RsaVerificationKey` | `(string keyId, RSA rsa, string? algorithmHint = null)` | Public key sufficient |
| `Ed25519VerificationKey` | `(string keyId, byte[] publicKeyBytes)` | `ed25519` ⚠️ stub |

All key types carry a `KeyId` (used in the `keyid` signature parameter) and an optional `AlgorithmHint`.

## Runtime Resolution

For server-side verification where the key and algorithm are not known in advance, implement `IKeyResolver` and use `SignatureAlgorithmRegistry`:

```csharp
// Implement key resolution from your keystore
public class MyKeyResolver : IKeyResolver
{
    public Task<VerificationKey?> ResolveKeyAsync(string keyId, CancellationToken ct = default)
    {
        // look up key by keyId
        var key = _store.Find(keyId);
        return Task.FromResult<VerificationKey?>(key);
    }
}

// Register the algorithms you accept
var registry = new SignatureAlgorithmRegistry();
registry.Register(new HmacSha256SignatureAlgorithm());
registry.Register(new EcdsaP256Sha256SignatureAlgorithm());

// Verify — algorithm and key are resolved from the Signature-Input header
var verifier = new HttpMessageVerifier();
var result = await verifier.VerifyAsync("sig1", context, new MyKeyResolver(), registry);
```

The `alg` and `keyid` parameters must be present in the `Signature-Input` header when using `VerifyAsync`.
