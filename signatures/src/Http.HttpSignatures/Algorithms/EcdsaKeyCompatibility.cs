using System.Security.Cryptography;
using DamianH.Http.HttpSignatures.Keys;

namespace DamianH.Http.HttpSignatures.Algorithms;

internal static class EcdsaKeyCompatibility
{
    internal static bool IsSigningKey(SigningKey key, string curveOid) =>
        key is EcdsaSigningKey ecdsaKey && HasCurve(ecdsaKey.Ecdsa, curveOid);

    internal static bool IsVerificationKey(VerificationKey key, string curveOid) =>
        key is EcdsaVerificationKey ecdsaKey && HasCurve(ecdsaKey.Ecdsa, curveOid);

    private static bool HasCurve(ECDsa ecdsa, string curveOid) =>
        string.Equals(ecdsa.ExportParameters(false).Curve.Oid.Value, curveOid, StringComparison.Ordinal);
}
