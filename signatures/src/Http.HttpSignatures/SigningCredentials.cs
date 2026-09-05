namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Binds signing key material to the single trusted algorithm that may use it.
/// </summary>
public sealed class SigningCredentials
{
    /// <summary>Initializes signing credentials.</summary>
    public SigningCredentials(SigningKey key, ISignatureAlgorithm algorithm)
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

    /// <summary>Gets the trusted signing key.</summary>
    public SigningKey Key { get; }

    /// <summary>Gets the trusted signature algorithm.</summary>
    public ISignatureAlgorithm Algorithm { get; }
}
