using System.Security.Cryptography;
using DamianH.Http.HttpSignatures.Algorithms;
using DamianH.Http.HttpSignatures.Keys;
using Shouldly;

namespace DamianH.Http.HttpSignatures;

public sealed class CredentialsTests
{
    [Fact]
    public void SigningCredentials_BindCompatibleKeyAndAlgorithm()
    {
        var algorithm = new HmacSha256SignatureAlgorithm();

        var credentials = new SigningCredentials(RfcTestKeys.HmacSharedSigningKey, algorithm);

        credentials.Key.ShouldBeSameAs(RfcTestKeys.HmacSharedSigningKey);
        credentials.Algorithm.ShouldBeSameAs(algorithm);
    }

    [Fact]
    public void VerificationCredentials_RejectWrongKeyFamily()
    {
        Should.Throw<ArgumentException>(() =>
            new VerificationCredentials(
                RfcTestKeys.HmacSharedVerificationKey,
                new RsaPssSha512SignatureAlgorithm()));
    }

    [Fact]
    public void EcdsaP256_RejectsP384Key()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        var key = new EcdsaVerificationKey("p384", ecdsa);

        new EcdsaP256Sha256SignatureAlgorithm().IsCompatible(key).ShouldBeFalse();
        Should.Throw<ArgumentException>(() =>
            new VerificationCredentials(key, new EcdsaP256Sha256SignatureAlgorithm()));
    }

    [Fact]
    public void EcdsaP384_RejectsP256Key()
    {
        new EcdsaP384Sha384SignatureAlgorithm()
            .IsCompatible(RfcTestKeys.EcdsaP256VerificationKey)
            .ShouldBeFalse();
    }

    [Fact]
    public void EcdsaP256_VerifyRejectsWrongSignatureLength()
    {
        new EcdsaP256Sha256SignatureAlgorithm()
            .Verify("data"u8, RfcTestKeys.EcdsaP256VerificationKey, new byte[63])
            .ShouldBeFalse();
    }

    [Fact]
    public void HmacKeys_DefensivelyCopySecretBytes()
    {
        byte[] secret = [1, 2, 3];
        var key = new HmacSharedKey("key", secret);
        secret[0] = 9;

        key.KeyBytes.ToArray().ShouldBe([1, 2, 3]);
    }
}
