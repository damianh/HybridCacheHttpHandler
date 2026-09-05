// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using DamianH.Http.HttpSignatures.Algorithms;
using DamianH.Http.HttpSignatures.Keys;
using Shouldly;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Tests for <see cref="EcdsaP384Sha384SignatureAlgorithm"/>.
/// No RFC test vectors exist for P-384, so we test sign/verify round-trips.
/// </summary>
public sealed class EcdsaP384Sha384Tests
{
    private static readonly EcdsaP384Sha384SignatureAlgorithm Algorithm = new();

    // Generate a P-384 key pair for testing (no RFC test key for P-384)
    private static EcdsaSigningKey CreateP384SigningKey()
    {
        var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        return new EcdsaSigningKey("test-key-p384", ecdsa);
    }

    private static EcdsaVerificationKey CreateP384VerificationKey(ECDsa ecdsa)
    {
        // Export/import public key only
        var pub = ECDsa.Create();
        pub.ImportSubjectPublicKeyInfo(ecdsa.ExportSubjectPublicKeyInfo(), out _);
        return new EcdsaVerificationKey("test-key-p384", pub);
    }

    [Fact]
    public void AlgorithmName_IsCorrect() =>
        Algorithm.AlgorithmName.ShouldBe("ecdsa-p384-sha384");

    [Fact]
    public void SignAndVerify_RoundTrip()
    {
        var signingKey = CreateP384SigningKey();
        var verificationKey = CreateP384VerificationKey(signingKey.Ecdsa);
        var data = "test data for ecdsa-p384"u8;

        var signature = Algorithm.Sign(data, signingKey);
        var result = Algorithm.Verify(data, verificationKey, signature);

        result.ShouldBeTrue();
    }

    [Fact]
    public void Sign_ProducesCorrectLength()
    {
        var key = CreateP384SigningKey();
        var data = "test"u8;

        var signature = Algorithm.Sign(data, key);

        // P-384 IEEE P1363 format: r (48 bytes) || s (48 bytes) = 96 bytes
        signature.Length.ShouldBe(96);
    }

    [Fact]
    public void Sign_IsNonDeterministic()
    {
        var key = CreateP384SigningKey();
        var data = "same data"u8;

        var sig1 = Algorithm.Sign(data, key);
        var sig2 = Algorithm.Sign(data, key);

        sig1.ShouldNotBe(sig2);
    }

    [Fact]
    public void Verify_TamperedData_ReturnsFalse()
    {
        var signingKey = CreateP384SigningKey();
        var verificationKey = CreateP384VerificationKey(signingKey.Ecdsa);

        var signature = Algorithm.Sign("original"u8, signingKey);

        Algorithm.Verify("tampered"u8, verificationKey, signature).ShouldBeFalse();
    }

    [Fact]
    public void Verify_TamperedSignature_ReturnsFalse()
    {
        var signingKey = CreateP384SigningKey();
        var verificationKey = CreateP384VerificationKey(signingKey.Ecdsa);
        var data = "test"u8;

        var signature = Algorithm.Sign(data, signingKey);
        var tampered = signature.ToArray();
        tampered[0] ^= 0xFF;

        Algorithm.Verify(data, verificationKey, tampered).ShouldBeFalse();
    }

    [Fact]
    public void Sign_WithWrongKeyType_ThrowsArgumentException()
    {
        var wrongKey = RfcTestKeys.HmacSharedSigningKey;
        byte[] data = [0x74, 0x65, 0x73, 0x74]; // "test"
        Should.Throw<ArgumentException>(() => Algorithm.Sign(data, wrongKey));
    }

    [Fact]
    public void Verify_WithWrongKeyType_ThrowsArgumentException()
    {
        var wrongKey = RfcTestKeys.HmacSharedVerificationKey;
        byte[] data = [0x74, 0x65, 0x73, 0x74]; // "test"
        Should.Throw<ArgumentException>(() => Algorithm.Verify(data, wrongKey, new byte[96]));
    }
}
