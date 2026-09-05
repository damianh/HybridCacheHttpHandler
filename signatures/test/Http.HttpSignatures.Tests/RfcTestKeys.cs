// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Numerics;
using System.Security.Cryptography;
using DamianH.Http.HttpSignatures.Keys;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Static helper providing all RFC 9421 Appendix B.1 test keys.
/// Keys are loaded once and cached for use across all algorithm test suites.
/// </summary>
internal static class RfcTestKeys
{
    // B.1.1 — test-key-rsa (PKCS#1 format)
    private const string RsaPublicKeyPem =
        """
        -----BEGIN RSA PUBLIC KEY-----
        MIIBCgKCAQEAhAKYdtoeoy8zcAcR874L8cnZxKzAGwd7v36APp7Pv6Q2jdsPBRrw
        WEBnez6d0UDKDwGbc6nxfEXAy5mbhgajzrw3MOEt8uA5txSKobBpKDeBLOsdJKFq
        MGmXCQvEG7YemcxDTRPxAleIAgYYRjTSd/QBwVW9OwNFhekro3RtlinV0a75jfZg
        kne/YiktSvLG34lw2zqXBDTC5NHROUqGTlML4PlNZS5Ri2U4aCNx2rUPRcKIlE0P
        uKxI4T+HIaFpv8+rdV6eUgOrB2xeI1dSFFn/nnv5OoZJEIB+VmuKn3DCUcCZSFlQ
        PSXSfBDiUGhwOw76WuSSsf1D4b/vLoJ10wIDAQAB
        -----END RSA PUBLIC KEY-----
        """;

    private const string RsaPrivateKeyPem =
        """
        -----BEGIN RSA PRIVATE KEY-----
        MIIEqAIBAAKCAQEAhAKYdtoeoy8zcAcR874L8cnZxKzAGwd7v36APp7Pv6Q2jdsP
        BRrwWEBnez6d0UDKDwGbc6nxfEXAy5mbhgajzrw3MOEt8uA5txSKobBpKDeBLOsd
        JKFqMGmXCQvEG7YemcxDTRPxAleIAgYYRjTSd/QBwVW9OwNFhekro3RtlinV0a75
        jfZgkne/YiktSvLG34lw2zqXBDTC5NHROUqGTlML4PlNZS5Ri2U4aCNx2rUPRcKI
        lE0PuKxI4T+HIaFpv8+rdV6eUgOrB2xeI1dSFFn/nnv5OoZJEIB+VmuKn3DCUcCZ
        SFlQPSXSfBDiUGhwOw76WuSSsf1D4b/vLoJ10wIDAQABAoIBAG/JZuSWdoVHbi56
        vjgCgkjg3lkO1KrO3nrdm6nrgA9P9qaPjxuKoWaKO1cBQlE1pSWp/cKncYgD5WxE
        CpAnRUXG2pG4zdkzCYzAh1i+c34L6oZoHsirK6oNcEnHveydfzJL5934egm6p8DW
        +m1RQ70yUt4uRc0YSor+q1LGJvGQHReF0WmJBZHrhz5e63Pq7lE0gIwuBqL8SMaA
        yRXtK+JGxZpImTq+NHvEWWCu09SCq0r838ceQI55SvzmTkwqtC+8AT2zFviMZkKR
        Qo6SPsrqItxZWRty2izawTF0Bf5S2VAx7O+6t3wBsQ1sLptoSgX3QblELY5asI0J
        YFz7LJECgYkAsqeUJmqXE3LP8tYoIjMIAKiTm9o6psPlc8CrLI9CH0UbuaA2JCOM
        cCNq8SyYbTqgnWlB9ZfcAm/cFpA8tYci9m5vYK8HNxQr+8FS3Qo8N9RJ8d0U5Csw
        DzMYfRghAfUGwmlWj5hp1pQzAuhwbOXFtxKHVsMPhz1IBtF9Y8jvgqgYHLbmyiu1
        mwJ5AL0pYF0G7x81prlARURwHo0Yf52kEw1dxpx+JXER7hQRWQki5/NsUEtv+8RT
        qn2m6qte5DXLyn83b1qRscSdnCCwKtKWUug5q2ZbwVOCJCtmRwmnP131lWRYfj67
        B/xJ1ZA6X3GEf4sNReNAtaucPEelgR2nsN0gKQKBiGoqHWbK1qYvBxX2X3kbPDkv
        9C+celgZd2PW7aGYLCHq7nPbmfDV0yHcWjOhXZ8jRMjmANVR/eLQ2EfsRLdW69bn
        f3ZD7JS1fwGnO3exGmHO3HZG+6AvberKYVYNHahNFEw5TsAcQWDLRpkGybBcxqZo
        81YCqlqidwfeO5YtlO7etx1xLyqa2NsCeG9A86UjG+aeNnXEIDk1PDK+EuiThIUa
        /2IxKzJKWl1BKr2d4xAfR0ZnEYuRrbeDQYgTImOlfW6/GuYIxKYgEKCFHFqJATAG
        IxHrq1PDOiSwXd2GmVVYyEmhZnbcp8CxaEMQoevxAta0ssMK3w6UsDtvUvYvF22m
        qQKBiD5GwESzsFPy3Ga0MvZpn3D6EJQLgsnrtUPZx+z2Ep2x0xc5orneB5fGyF1P
        WtP+fG5Q6Dpdz3LRfm+KwBCWFKQjg7uTxcjerhBWEYPmEMKYwTJF5PBG9/ddvHLQ
        EQeNC8fHGg4UXU8mhHnSBt3EA10qQJfRDs15M38eG2cYwB1PZpDHScDnDA0=
        -----END RSA PRIVATE KEY-----
        """;

    // B.1.2 — test-key-rsa-pss (PKCS#8 format)
    private const string RsaPssPublicKeyPem =
        """
        -----BEGIN PUBLIC KEY-----
        MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAr4tmm3r20Wd/PbqvP1s2
        +QEtvpuRaV8Yq40gjUR8y2Rjxa6dpG2GXHbPfvMs8ct+Lh1GH45x28Rw3Ry53mm+
        oAXjyQ86OnDkZ5N8lYbggD4O3w6M6pAvLkhk95AndTrifbIFPNU8PPMO7OyrFAHq
        gDsznjPFmTOtCEcN2Z1FpWgchwuYLPL+Wokqltd11nqqzi+bJ9cvSKADYdUAAN5W
        Utzdpiy6LbTgSxP7ociU4Tn0g5I6aDZJ7A8Lzo0KSyZYoA485mqcO0GVAdVw9lq4
        aOT9v6d+nb4bnNkQVklLQ3fVAvJm+xdDOp9LCNCN48V2pnDOkFV6+U9nV5oyc6XI
        2wIDAQAB
        -----END PUBLIC KEY-----
        """;

    private const string RsaPssPrivateKeyPem =
        """
        -----BEGIN PRIVATE KEY-----
        MIIEvgIBADALBgkqhkiG9w0BAQoEggSqMIIEpgIBAAKCAQEAr4tmm3r20Wd/Pbqv
        P1s2+QEtvpuRaV8Yq40gjUR8y2Rjxa6dpG2GXHbPfvMs8ct+Lh1GH45x28Rw3Ry5
        3mm+oAXjyQ86OnDkZ5N8lYbggD4O3w6M6pAvLkhk95AndTrifbIFPNU8PPMO7Oyr
        FAHqgDsznjPFmTOtCEcN2Z1FpWgchwuYLPL+Wokqltd11nqqzi+bJ9cvSKADYdUA
        AN5WUtzdpiy6LbTgSxP7ociU4Tn0g5I6aDZJ7A8Lzo0KSyZYoA485mqcO0GVAdVw
        9lq4aOT9v6d+nb4bnNkQVklLQ3fVAvJm+xdDOp9LCNCN48V2pnDOkFV6+U9nV5oy
        c6XI2wIDAQABAoIBAQCUB8ip+kJiiZVKF8AqfB/aUP0jTAqOQewK1kKJ/iQCXBCq
        pbo360gvdt05H5VZ/RDVkEgO2k73VSsbulqezKs8RFs2tEmU+JgTI9MeQJPWcP6X
        aKy6LIYs0E2cWgp8GADgoBs8llBq0UhX0KffglIeek3n7Z6Gt4YFge2TAcW2WbN4
        XfK7lupFyo6HHyWRiYHMMARQXLJeOSdTn5aMBP0PO4bQyk5ORxTUSeOciPJUFktQ
        HkvGbym7KryEfwH8Tks0L7WhzyP60PL3xS9FNOJi9m+zztwYIXGDQuKM2GDsITeD
        2mI2oHoPMyAD0wdI7BwSVW18p1h+jgfc4dlexKYRAoGBAOVfuiEiOchGghV5vn5N
        RDNscAFnpHj1QgMr6/UG05RTgmcLfVsI1I4bSkbrIuVKviGGf7atlkROALOG/xRx
        DLadgBEeNyHL5lz6ihQaFJLVQ0u3U4SB67J0YtVO3R6lXcIjBDHuY8SjYJ7Ci6Z6
        vuDcoaEujnlrtUhaMxvSfcUJAoGBAMPsCHXte1uWNAqYad2WdLjPDlKtQJK1diCm
        rqmB2g8QE99hDOHItjDBEdpyFBKOIP+NpVtM2KLhRajjcL9Ph8jrID6XUqikQuVi
        4J9FV2m42jXMuioTT13idAILanYg8D3idvy/3isDVkON0X3UAVKrgMEne0hJpkPL
        FYqgetvDAoGBAKLQ6JZMbSe0pPIJkSamQhsehgL5Rs51iX4m1z7+sYFAJfhvN3Q/
        OGIHDRp6HjMUcxHpHw7U+S1TETxePwKLnLKj6hw8jnX2/nZRgWHzgVcY+sPsReRx
        NJVf+Cfh6yOtznfX00p+JWOXdSY8glSSHJwRAMog+hFGW1AYdt7w80XBAoGBAImR
        NUugqapgaEA8TrFxkJmngXYaAqpA0iYRA7kv3S4QavPBUGtFJHBNULzitydkNtVZ
        3w6hgce0h9YThTo/nKc+OZDZbgfN9s7cQ75x0PQCAO4fx2P91Q+mDzDUVTeG30mE
        t2m3S0dGe47JiJxifV9P3wNBNrZGSIF3mrORBVNDAoGBAI0QKn2Iv7Sgo4T/XjND
        dl2kZTXqGAk8dOhpUiw/HdM3OGWbhHj2NdCzBliOmPyQtAr770GITWvbAI+IRYyF
        S7Fnk6ZVVVHsxjtaHy1uJGFlaZzKR4AGNaUTOJMs6NadzCmGPAxNQQOCqoUjn4XR
        rOjr9w349JooGXhOxbu8nOxX
        -----END PRIVATE KEY-----
        """;

    // B.1.3 — test-key-ecc-p256
    private const string EccP256PublicKeyPem =
        """
        -----BEGIN PUBLIC KEY-----
        MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEqIVYZVLCrPZHGHjP17CTW0/+D9Lf
        w0EkjqF7xB4FivAxzic30tMM4GF+hR6Dxh71Z50VGGdldkkDXZCnTNnoXQ==
        -----END PUBLIC KEY-----
        """;

    private const string EccP256PrivateKeyPem =
        """
        -----BEGIN EC PRIVATE KEY-----
        MHcCAQEEIFKbhfNZfpDsW43+0+JjUr9K+bTeuxopu653+hBaXGA7oAoGCCqGSM49
        AwEHoUQDQgAEqIVYZVLCrPZHGHjP17CTW0/+D9Lfw0EkjqF7xB4FivAxzic30tMM
        4GF+hR6Dxh71Z50VGGdldkkDXZCnTNnoXQ==
        -----END EC PRIVATE KEY-----
        """;

    // B.1.4 — test-key-ed25519 (PKCS#8 format)
    private const string Ed25519PublicKeyPem =
        """
        -----BEGIN PUBLIC KEY-----
        MCowBQYDK2VwAyEAJrQLj5P/89iXES9+vFgrIy29clF9CC/oPPsw3c5D0bs=
        -----END PUBLIC KEY-----
        """;

    private const string Ed25519PrivateKeyPem =
        """
        -----BEGIN PRIVATE KEY-----
        MC4CAQAwBQYDK2VwBCIEIJ+DYvh6SEqVTm50DFtMDoQikTmiCqirVv9mWG9qfSnF
        -----END PRIVATE KEY-----
        """;

    // B.1.5 — test-shared-secret (64 random bytes, Base64 encoded)
    private const string SharedSecretBase64 =
        "uzvJfB4u3N0Jy4T7NZ75MDVcr8zSTInedJtkgcu46YW4XByzNJjxBdtjUkdJPBtbmHhIDi6pcl8jsasjlTMtDQ==";

    /// <summary>
    /// Gets the HMAC shared secret (B.1.5) as a signing key.
    /// </summary>
    public static HmacSharedKey HmacSharedSigningKey { get; } =
        new("test-shared-secret", Convert.FromBase64String(SharedSecretBase64));

    /// <summary>
    /// Gets the HMAC shared secret (B.1.5) as a verification key.
    /// </summary>
    public static HmacSharedVerificationKey HmacSharedVerificationKey { get; } =
        HmacSharedSigningKey.AsVerificationKey();

    /// <summary>
    /// Gets the RSA-PSS signing key (B.1.2).
    /// </summary>
    public static RsaSigningKey RsaPssSigningKey { get; } = CreateRsaPssSigningKey();

    /// <summary>
    /// Gets the RSA-PSS verification key (B.1.2).
    /// </summary>
    public static RsaVerificationKey RsaPssVerificationKey { get; } = CreateRsaPssVerificationKey();

    /// <summary>
    /// Gets the RSA signing key (B.1.1) for rsa-v1_5-sha256.
    /// </summary>
    public static RsaSigningKey RsaSigningKey { get; } = CreateRsaSigningKey();

    /// <summary>
    /// Gets the RSA verification key (B.1.1) for rsa-v1_5-sha256.
    /// </summary>
    public static RsaVerificationKey RsaVerificationKey { get; } = CreateRsaVerificationKey();

    /// <summary>
    /// Gets the ECDSA P-256 signing key (B.1.3).
    /// </summary>
    public static EcdsaSigningKey EcdsaP256SigningKey { get; } = CreateEcdsaP256SigningKey();

    /// <summary>
    /// Gets the ECDSA P-256 verification key (B.1.3).
    /// </summary>
    public static EcdsaVerificationKey EcdsaP256VerificationKey { get; } = CreateEcdsaP256VerificationKey();

    /// <summary>
    /// Gets the raw Ed25519 public key bytes (B.1.4), extracted from the PKCS#8 PEM.
    /// 32 bytes of the raw public key.
    /// </summary>
    public static byte[] Ed25519PublicKeyBytes { get; } = ExtractEd25519PublicKeyBytes();

    /// <summary>
    /// Gets the raw Ed25519 private key bytes (B.1.4), extracted from the PKCS#8 PEM.
    /// 32 bytes of the raw private key seed.
    /// </summary>
    public static byte[] Ed25519PrivateKeyBytes { get; } = ExtractEd25519PrivateKeyBytes();

    /// <summary>
    /// Gets the Ed25519 signing key (B.1.4).
    /// </summary>
    public static Ed25519SigningKey Ed25519Signing { get; } =
        new("test-key-ed25519", Ed25519PrivateKeyBytes);

    /// <summary>
    /// Gets the Ed25519 verification key (B.1.4).
    /// </summary>
    public static Ed25519VerificationKey Ed25519Verification { get; } =
        new("test-key-ed25519", Ed25519PublicKeyBytes);

    private static RsaSigningKey CreateRsaPssSigningKey()
    {
        // .NET's RSA.ImportFromPem rejects PKCS#8 keys with OID rsaPSS (1.2.840.113549.1.1.10).
        // The RFC B.1.2 test-key-rsa-pss private key uses this OID.
        // Workaround: strip PEM armor, decode to DER, and extract the inner RSA private key
        // from the PKCS#8 PrivateKeyInfo structure, then import with ImportRSAPrivateKey.
        var rsa = RSA.Create();
        var derBytes = PemToDer(RsaPssPrivateKeyPem, "PRIVATE KEY");
        var innerKey = ExtractPrivateKeyFromPkcs8(derBytes);
        rsa.ImportRSAPrivateKey(innerKey, out _);
        return new RsaSigningKey("test-key-rsa-pss", rsa);
    }

    private static RsaVerificationKey CreateRsaPssVerificationKey()
    {
        // The RSA-PSS public key uses standard SPKI format with RSA OID — ImportFromPem works fine.
        var rsa = RSA.Create();
        rsa.ImportFromPem(RsaPssPublicKeyPem);
        return new RsaVerificationKey("test-key-rsa-pss", rsa);
    }

    private static RsaSigningKey CreateRsaSigningKey()
    {
        // The RFC B.1.1 RSA private key has non-standard CRT parameter sizes
        // (P=1088 bits, Q=960 bits instead of the standard ~1024 bits each).
        // .NET's RSA implementation strictly requires CRT params to be exactly
        // ceil(modulusLen/2) bytes, so both PEM import and RSAParameters import fail.
        // Workaround: use a software RSA implementation that does BigInteger.ModPow
        // directly with just N, D, E (no CRT params needed).
        var rsa = new SoftwareRsa(
            FromBase64Url(RsaModulusB64),
            FromBase64Url(RsaExponentB64),
            FromBase64Url(RsaPrivateExponentB64));
        return new RsaSigningKey("test-key-rsa", rsa);
    }

    private static RsaVerificationKey CreateRsaVerificationKey()
    {
        // The public key (Modulus + Exponent only) imports fine into .NET RSA.
        var rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters
        {
            Modulus = FromBase64Url(RsaModulusB64),
            Exponent = FromBase64Url(RsaExponentB64),
        });
        return new RsaVerificationKey("test-key-rsa", rsa);
    }

    // RFC 9421 Appendix B.1.1 JWK values (base64url-encoded, RFC 8792 line wrapping removed).
    private const string RsaModulusB64 =
        "hAKYdtoeoy8zcAcR874L8cnZxKzAGwd7v36APp7Pv6Q2jdsPBRrwWEBnez6" +
        "d0UDKDwGbc6nxfEXAy5mbhgajzrw3MOEt8uA5txSKobBpKDeBLOsdJKFqMGmXCQvE" +
        "G7YemcxDTRPxAleIAgYYRjTSd_QBwVW9OwNFhekro3RtlinV0a75jfZgkne_YiktS" +
        "vLG34lw2zqXBDTC5NHROUqGTlML4PlNZS5Ri2U4aCNx2rUPRcKIlE0PuKxI4T-HIa" +
        "Fpv8-rdV6eUgOrB2xeI1dSFFn_nnv5OoZJEIB-VmuKn3DCUcCZSFlQPSXSfBDiUGh" +
        "wOw76WuSSsf1D4b_vLoJ10w";

    private const string RsaExponentB64 = "AQAB";

    private const string RsaPrivateExponentB64 =
        "b8lm5JZ2hUduLnq-OAKCSODeWQ7Uqs7eet2bqeuAD0_2po-PG4qhZoo7VwF" +
        "CUTWlJan9wqdxiAPlbEQKkCdFRcbakbjN2TMJjMCHWL5zfgvqhmgeyKsrqg1wSce9" +
        "7J1_Mkvn3fh6CbqnwNb6bVFDvTJS3i5FzRhKiv6rUsYm8ZAdF4XRaYkFkeuHPl7rc" +
        "-ruUTSAjC4GovxIxoDJFe0r4kbFmkiZOr40e8RZYK7T1IKrSvzfxx5AjnlK_OZOTC" +
        "q0L7wBPbMW-IxmQpFCjpI-yuoi3FlZG3LaLNrBMXQF_lLZUDHs77q3fAGxDWwum2h" +
        "KBfdBuUQtjlqwjQlgXPsskQ";

    private static byte[] FromBase64Url(string base64Url)
    {
        var clean = base64Url.Replace('-', '+').Replace('_', '/');
        switch (clean.Length % 4)
        {
            case 2: clean += "=="; break;
            case 3: clean += "="; break;
        }

        return Convert.FromBase64String(clean);
    }

    /// <summary>
    /// Software RSA implementation using <see cref="BigInteger.ModPow"/> for signing.
    /// Required because the RFC 9421 B.1.1 test key has non-standard prime sizes
    /// (P=1088 bits, Q=960 bits) that .NET CNG rejects.
    /// Only PKCS#1 v1.5 signing with SHA-256 is supported — sufficient for test vectors.
    /// </summary>
    private sealed class SoftwareRsa : RSA
    {
        private static readonly byte[] Sha256DigestInfoPrefix =
        [
            0x30, 0x31, 0x30, 0x0d, 0x06, 0x09, 0x60, 0x86,
            0x48, 0x01, 0x65, 0x03, 0x04, 0x02, 0x01, 0x05,
            0x00, 0x04, 0x20,
        ];

        private readonly BigInteger _n;
        private readonly BigInteger _d;
        private readonly int _modulusBytes;

        public SoftwareRsa(byte[] modulus, byte[] exponent, byte[] privateExponent)
        {
            _n = BytesToBigInt(modulus);
            _d = BytesToBigInt(privateExponent);
            _modulusBytes = modulus.Length;
            KeySizeValue = modulus.Length * 8;
        }

        public override RSAParameters ExportParameters(bool includePrivateParameters) =>
            throw new NotSupportedException("SoftwareRsa does not support parameter export.");

        public override void ImportParameters(RSAParameters parameters) =>
            throw new NotSupportedException("SoftwareRsa does not support parameter import.");

        public override byte[] SignHash(
            byte[] hash,
            HashAlgorithmName hashAlgorithm,
            RSASignaturePadding padding)
        {
            if (padding != RSASignaturePadding.Pkcs1)
            {
                throw new NotSupportedException(
                    $"Only PKCS#1 v1.5 padding is supported, got {padding}.");
            }

            var digestInfoPrefix = hashAlgorithm.Name switch
            {
                "SHA256" => Sha256DigestInfoPrefix,
                _ => throw new NotSupportedException(
                    $"Hash algorithm {hashAlgorithm.Name} is not supported."),
            };

            // EMSA-PKCS1-v1_5 encoding: 0x00 0x01 [0xFF padding] 0x00 [DigestInfo]
            var digestInfoLen = digestInfoPrefix.Length + hash.Length;
            var em = new byte[_modulusBytes];
            em[1] = 0x01;
            var padLen = _modulusBytes - digestInfoLen - 3;
            em.AsSpan(2, padLen).Fill(0xFF);
            // em[2 + padLen] is already 0x00
            digestInfoPrefix.CopyTo(em, 3 + padLen);
            hash.CopyTo(em, 3 + padLen + digestInfoPrefix.Length);

            var m = BytesToBigInt(em);
            var s = BigInteger.ModPow(m, _d, _n);
            return BigIntToBytes(s, _modulusBytes);
        }

        public override bool VerifyHash(
            byte[] hash,
            byte[] signature,
            HashAlgorithmName hashAlgorithm,
            RSASignaturePadding padding) =>
            throw new NotSupportedException("Use the public key for verification.");

        private static BigInteger BytesToBigInt(byte[] bytes)
        {
            // BigInteger constructor expects little-endian; append 0x00 for unsigned.
            var le = new byte[bytes.Length + 1];
            Array.Copy(bytes, le, bytes.Length);
            Array.Reverse(le, 0, bytes.Length);
            return new BigInteger(le);
        }

        private static byte[] BigIntToBytes(BigInteger value, int targetLength)
        {
            var bytes = value.ToByteArray(); // little-endian, signed
            var result = new byte[targetLength];
            var copyLen = Math.Min(bytes.Length, targetLength);
            if (bytes.Length > targetLength && bytes[bytes.Length - 1] == 0)
            {
                copyLen = targetLength;
            }

            for (var i = 0; i < copyLen; i++)
            {
                result[targetLength - 1 - i] = bytes[i];
            }

            return result;
        }
    }

    private static EcdsaSigningKey CreateEcdsaP256SigningKey()
    {
        var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(EccP256PrivateKeyPem);
        return new EcdsaSigningKey("test-key-ecc-p256", ecdsa);
    }

    private static EcdsaVerificationKey CreateEcdsaP256VerificationKey()
    {
        var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(EccP256PublicKeyPem);
        return new EcdsaVerificationKey("test-key-ecc-p256", ecdsa);
    }

    /// <summary>
    /// Strips PEM armor and Base64-decodes to DER bytes.
    /// </summary>
    private static byte[] PemToDer(string pem, string label)
    {
        var base64 = pem
            .Replace($"-----BEGIN {label}-----", "")
            .Replace($"-----END {label}-----", "")
            .Replace("\r", "")
            .Replace("\n", "")
            .Trim();
        return Convert.FromBase64String(base64);
    }

    /// <summary>
    /// Extracts the inner RSA private key (PKCS#1 RSAPrivateKey) from a PKCS#8 PrivateKeyInfo structure.
    /// PKCS#8 structure: SEQUENCE { INTEGER version, SEQUENCE algorithmIdentifier, OCTET STRING privateKey }
    /// The privateKey OCTET STRING contains the RSA private key in PKCS#1 format.
    /// </summary>
    private static byte[] ExtractPrivateKeyFromPkcs8(byte[] pkcs8Der)
    {
        // Minimal ASN.1 DER parser — just enough to skip to the third element
        // of the outer SEQUENCE (the OCTET STRING containing the private key).
        var offset = 0;

        // Outer SEQUENCE
        ReadTagAndLength(pkcs8Der, ref offset);

        // INTEGER (version) — skip
        SkipTlv(pkcs8Der, ref offset);

        // SEQUENCE (algorithmIdentifier) — skip
        SkipTlv(pkcs8Der, ref offset);

        // OCTET STRING (privateKey) — read its contents
        if (pkcs8Der[offset] != 0x04)
            throw new CryptographicException("Expected OCTET STRING tag (0x04) for private key.");
        offset++; // skip tag
        var length = ReadLength(pkcs8Der, ref offset);
        return pkcs8Der.AsSpan(offset, length).ToArray();
    }

    private static int ReadTagAndLength(byte[] data, ref int offset)
    {
        offset++; // skip tag
        return ReadLength(data, ref offset);
    }

    private static int ReadLength(byte[] data, ref int offset)
    {
        var b = data[offset++];
        if (b < 0x80)
            return b;
        var numBytes = b & 0x7F;
        var length = 0;
        for (var i = 0; i < numBytes; i++)
        {
            length = (length << 8) | data[offset++];
        }
        return length;
    }

    private static void SkipTlv(byte[] data, ref int offset)
    {
        offset++; // skip tag
        var length = ReadLength(data, ref offset);
        offset += length;
    }

    private static byte[] ExtractEd25519PublicKeyBytes()
    {
        // The PKCS#8 SubjectPublicKeyInfo for Ed25519 is:
        // SEQUENCE { SEQUENCE { OID 1.3.101.112 }, BIT STRING { <32 bytes> } }
        // The PEM base64 decodes to 44 bytes; the last 32 are the raw public key.
        var lines = Ed25519PublicKeyPem
            .Replace("-----BEGIN PUBLIC KEY-----", "")
            .Replace("-----END PUBLIC KEY-----", "")
            .Replace("\r", "")
            .Replace("\n", "");
        var der = Convert.FromBase64String(lines);
        // Ed25519 SubjectPublicKeyInfo is exactly 44 bytes; raw key is last 32
        return der[^32..];
    }

    private static byte[] ExtractEd25519PrivateKeyBytes()
    {
        // The PKCS#8 PrivateKeyInfo for Ed25519 is:
        // SEQUENCE { INTEGER 0, SEQUENCE { OID 1.3.101.112 }, OCTET STRING { OCTET STRING { <32 bytes> } } }
        // The PEM base64 decodes to 48 bytes; the last 32 are the raw private key seed.
        var lines = Ed25519PrivateKeyPem
            .Replace("-----BEGIN PRIVATE KEY-----", "")
            .Replace("-----END PRIVATE KEY-----", "")
            .Replace("\r", "")
            .Replace("\n", "");
        var der = Convert.FromBase64String(lines);
        // Ed25519 PKCS#8 PrivateKeyInfo is exactly 48 bytes; raw seed is last 32
        return der[^32..];
    }
}
