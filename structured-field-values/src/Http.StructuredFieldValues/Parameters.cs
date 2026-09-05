// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace DamianH.Http.StructuredFieldValues;

/// <summary>
/// Represents parameters attached to items or inner lists in structured field values.
/// Parameters are an ordered map of valid keys to non-null immutable bare values.
/// A flag is Boolean true, not null. This mutable collection uses reference equality.
/// </summary>
public sealed class Parameters : IEnumerable<KeyValuePair<string, BareItem>>
{
    private readonly OrderedDictionary<string, BareItem> _parameters = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="Parameters"/> class.
    /// </summary>
    public Parameters()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Parameters"/> class with initial values.
    /// </summary>
    /// <param name="parameters">Initial parameters.</param>
    public Parameters(IEnumerable<KeyValuePair<string, BareItem>> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        
        foreach (var kvp in parameters)
        {
            Add(kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    /// Gets the number of parameters.
    /// </summary>
    public int Count => _parameters.Count;

    /// <summary>
    /// Gets or sets the parameter with the specified key.
    /// </summary>
    /// <param name="key">The parameter key (must be a valid token).</param>
    /// <returns>The non-null bare parameter value.</returns>
    public BareItem this[string key]
    {
        get => _parameters[key];
        set
        {
            ValidateKey(key);
            ArgumentNullException.ThrowIfNull(value);
            _parameters[key] = value;
        }
    }

    /// <summary>
    /// Adds a parameter with the specified key and value.
    /// </summary>
    /// <param name="key">The parameter key (must be a valid token).</param>
    /// <param name="value">The parameter value.</param>
    public void Add(string key, BareItem value)
    {
        ValidateKey(key);
        ArgumentNullException.ThrowIfNull(value);
        _parameters.Add(key, value);
    }

    /// <summary>Adds a Boolean true flag. Duplicate keys throw.</summary>
    public void Add(string key) => Add(key, BooleanItem.True);

    /// <summary>
    /// Tries to get the parameter with the specified key.
    /// </summary>
    /// <param name="key">The parameter key.</param>
    /// <param name="value">The parameter value if found.</param>
    /// <returns>True if the parameter exists, false otherwise.</returns>
    public bool TryGetValue(string key, [NotNullWhen(true)] out BareItem? value) => _parameters.TryGetValue(key, out value);

    /// <summary>
    /// Determines whether a parameter with the specified key exists.
    /// </summary>
    /// <param name="key">The parameter key.</param>
    /// <returns>True if the parameter exists, false otherwise.</returns>
    public bool ContainsKey(string key) => _parameters.ContainsKey(key);

    /// <summary>
    /// Removes the parameter with the specified key.
    /// </summary>
    /// <param name="key">The parameter key.</param>
    /// <returns>True if the parameter was removed, false otherwise.</returns>
    public bool Remove(string key) => _parameters.Remove(key);

    /// <summary>
    /// Removes all parameters.
    /// </summary>
    public void Clear() => _parameters.Clear();

    /// <summary>
    /// Gets an enumerator that iterates through the parameters in insertion order.
    /// </summary>
    public IEnumerator<KeyValuePair<string, BareItem>> GetEnumerator() => _parameters.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Returns diagnostic text, not wire output; use <see cref="StructuredFieldSerializer"/>.</summary>
    public override string ToString() => $"Parameters({Count})";

    private static void ValidateKey(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        if (!TokenItem.IsValidKey(key))
        {
            throw new ArgumentException(
                $"Parameter key '{key}' is not a valid RFC 9651 key. " +
                "Keys must start with a lowercase letter or '*' and contain only " +
                "lowercase letters, digits, '_', '-', '.', or '*'.",
                nameof(key));
        }
    }
}
