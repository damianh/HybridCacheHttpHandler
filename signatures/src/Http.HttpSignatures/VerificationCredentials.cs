namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Binds verification key material to the single trusted algorithm that may use it.
/// </summary>
public sealed class VerificationCredentials
{
    /// <summary>Initializes verification credentials.</summary>
    public VerificationCredentials(VerificationKey key, ISignatureAlgorithm algorithm)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(algorithm);

        if (!algorithm.IsCompatible(key))
        {
            throw new ArgumentException(
                $"Key '{key.KeyId}' is not compatible with algorithm '{algorithm.AlgorithmName}'.",
                nameof(key));
        }

        Key = key;
        Algorithm = algorithm;
    }

    /// <summary>Gets the trusted verification key.</summary>
    public VerificationKey Key { get; }

    /// <summary>Gets the trusted signature algorithm.</summary>
    public ISignatureAlgorithm Algorithm { get; }
}
