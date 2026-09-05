// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// The Structured Field Values (RFC 8941/9651) top-level type of an HTTP field, as registered
/// for that field name. RFC 9421 §2.1.1/§2.1.2 require this to be known in advance for the
/// <c>sf</c> and <c>key</c> component parameters; it is never guessed from the wire value.
/// </summary>
public enum StructuredFieldValueKind
{
    /// <summary>The field's Structured Field type is not declared.</summary>
    Unknown,

    /// <summary>The field is a Structured Field Item (RFC 8941 §3.3), optionally with parameters.</summary>
    Item,

    /// <summary>The field is a Structured Field List (RFC 8941 §3.1).</summary>
    List,

    /// <summary>The field is a Structured Field Dictionary (RFC 8941 §3.2).</summary>
    Dictionary,
}

/// <summary>
/// Declares the Structured Field Values type of HTTP fields, so that <c>sf</c> and <c>key</c>
/// component processing (RFC 9421 §2.1.1, §2.1.2) can select the correct parser deterministically
/// instead of guessing the type by trying each parser in turn.
/// </summary>
public interface IStructuredFieldTypeResolver
{
    /// <summary>
    /// Resolves the declared Structured Field type for the named field.
    /// </summary>
    /// <param name="isRequest">Whether the field belongs to a request message.</param>
    /// <param name="fieldName">The lowercase field name.</param>
    /// <returns>
    /// The declared type, or <see cref="StructuredFieldValueKind.Unknown"/> when the field's type
    /// is not registered. <see cref="StructuredFieldValueKind.Unknown"/> is always an error when
    /// typed processing is requested; it never falls back to guessing the type from the value.
    /// </returns>
    StructuredFieldValueKind ResolveType(bool isRequest, string fieldName);
}
