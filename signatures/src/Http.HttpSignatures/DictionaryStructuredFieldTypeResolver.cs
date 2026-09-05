// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// An <see cref="IStructuredFieldTypeResolver"/> backed by a static, caller-supplied map of
/// field name to declared Structured Field type. Field names are matched case-insensitively.
/// The same declared types apply to both request and response fields.
/// </summary>
public sealed class DictionaryStructuredFieldTypeResolver : IStructuredFieldTypeResolver
{
    private readonly IReadOnlyDictionary<string, StructuredFieldValueKind> _types;

    /// <summary>
    /// Initializes a new instance of the <see cref="DictionaryStructuredFieldTypeResolver"/> class.
    /// </summary>
    /// <param name="fieldTypes">The map of field name to declared Structured Field type.</param>
    public DictionaryStructuredFieldTypeResolver(IReadOnlyDictionary<string, StructuredFieldValueKind> fieldTypes)
    {
        ArgumentNullException.ThrowIfNull(fieldTypes);

        // Defensive copy; case-insensitive per RFC 9110 §5.1 field-name comparison.
        _types = new Dictionary<string, StructuredFieldValueKind>(fieldTypes, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public StructuredFieldValueKind ResolveType(bool isRequest, string fieldName) =>
        _types.TryGetValue(fieldName, out var kind) ? kind : StructuredFieldValueKind.Unknown;
}
