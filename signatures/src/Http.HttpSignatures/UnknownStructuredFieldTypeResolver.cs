// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// The default <see cref="IStructuredFieldTypeResolver"/> used when none is supplied: every field's
/// type is unknown. This makes <c>sf</c>/<c>key</c> processing fail explicitly rather than silently
/// falling back to guessing the Structured Field type by trying each parser in turn.
/// </summary>
internal sealed class UnknownStructuredFieldTypeResolver : IStructuredFieldTypeResolver
{
    public static readonly UnknownStructuredFieldTypeResolver Instance = new();

    private UnknownStructuredFieldTypeResolver()
    {
    }

    public StructuredFieldValueKind ResolveType(bool isRequest, string fieldName) => StructuredFieldValueKind.Unknown;
}
