// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using DamianH.Http.StructuredFieldValues;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Represents a component identifier per RFC 9421 §2.
/// Combines a component name with optional parameters (sf, key, bs, req, tr, name).
/// Component identifiers appear in the covered components list of a signature.
/// </summary>
public sealed class ComponentIdentifier : IEquatable<ComponentIdentifier>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ComponentIdentifier"/> class.
    /// </summary>
    /// <param name="name">The component name (e.g., "@method", "content-type").</param>
    public ComponentIdentifier(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name.ToLowerInvariant();
    }

    /// <summary>Gets the component name (e.g., "@method", "content-type").</summary>
    public string Name { get; }

    /// <summary>
    /// Gets a value indicating whether to use strict structured field serialization.
    /// RFC 9421 §2.1 — the <c>sf</c> parameter.
    /// </summary>
    public bool Sf { get; init; }

    /// <summary>
    /// Gets the dictionary member key for structured field extraction.
    /// RFC 9421 §2.1.2 — the <c>key</c> parameter.
    /// </summary>
    public string? Key { get; init; }

    /// <summary>
    /// Gets a value indicating whether to use binary-wrapped encoding.
    /// RFC 9421 §2.1.3 — the <c>bs</c> parameter.
    /// </summary>
    public bool Bs { get; init; }

    /// <summary>
    /// Gets a value indicating whether the component is taken from the associated request.
    /// RFC 9421 §2.4 — the <c>req</c> parameter.
    /// </summary>
    public bool Req { get; init; }

    /// <summary>
    /// Gets a value indicating whether the component is taken from trailers.
    /// RFC 9421 §2.1.4 — the <c>tr</c> parameter.
    /// </summary>
    public bool Tr { get; init; }

    /// <summary>
    /// Gets the query parameter name for <c>@query-param</c> components.
    /// RFC 9421 §2.2.8 — the <c>name</c> parameter.
    /// </summary>
    public string? QueryParamName { get; init; }

    /// <summary>Gets a value indicating whether this is a derived component (starts with '@').</summary>
    public bool IsDerived => Name.StartsWith('@');

    /// <summary>
    /// Creates a component identifier for an HTTP field.
    /// </summary>
    /// <param name="fieldName">The lowercase field name.</param>
    public static ComponentIdentifier Field(string fieldName) => new(fieldName);

    /// <summary>
    /// Creates a component identifier for an HTTP field with strict SF serialization.
    /// </summary>
    /// <param name="fieldName">The lowercase field name.</param>
    public static ComponentIdentifier FieldSf(string fieldName) => new(fieldName) { Sf = true };

    /// <summary>
    /// Creates a component identifier for a specific key in an SF Dictionary field.
    /// </summary>
    /// <param name="fieldName">The lowercase field name.</param>
    /// <param name="key">The dictionary key to extract.</param>
    public static ComponentIdentifier FieldKey(string fieldName, string key) => new(fieldName) { Key = key };

    /// <summary>
    /// Creates a component identifier for a binary-wrapped HTTP field.
    /// </summary>
    /// <param name="fieldName">The lowercase field name.</param>
    public static ComponentIdentifier FieldBs(string fieldName) => new(fieldName) { Bs = true };

    /// <summary>Gets the <c>@method</c> derived component identifier.</summary>
    public static ComponentIdentifier Method { get; } = new("@method");

    /// <summary>Gets the <c>@authority</c> derived component identifier.</summary>
    public static ComponentIdentifier Authority { get; } = new("@authority");

    /// <summary>Gets the <c>@scheme</c> derived component identifier.</summary>
    public static ComponentIdentifier Scheme { get; } = new("@scheme");

    /// <summary>Gets the <c>@target-uri</c> derived component identifier.</summary>
    public static ComponentIdentifier TargetUri { get; } = new("@target-uri");

    /// <summary>Gets the <c>@request-target</c> derived component identifier.</summary>
    public static ComponentIdentifier RequestTarget { get; } = new("@request-target");

    /// <summary>Gets the <c>@path</c> derived component identifier.</summary>
    public static ComponentIdentifier Path { get; } = new("@path");

    /// <summary>Gets the <c>@query</c> derived component identifier.</summary>
    public static ComponentIdentifier Query { get; } = new("@query");

    /// <summary>Gets the <c>@status</c> derived component identifier (responses only).</summary>
    public static ComponentIdentifier Status { get; } = new("@status");

    /// <summary>
    /// Creates a <c>@query-param</c> derived component identifier for a specific query parameter.
    /// </summary>
    /// <param name="paramName">The name of the query parameter.</param>
    public static ComponentIdentifier QueryParam(string paramName) =>
        new("@query-param") { QueryParamName = paramName };

    /// <summary>
    /// Serializes this component identifier to the format used in the signature base and Signature-Input.
    /// The name is serialized as an SF String (quoted), followed by any parameters.
    /// </summary>
    /// <returns>The serialized component identifier, e.g. <c>"@method"</c> or <c>"content-digest";req</c>.</returns>
    public string Serialize() => StructuredFieldSerializer.SerializeItem(ToStructuredFieldItem());

    internal StructuredFieldItem ToStructuredFieldItem()
    {
        var item = new StructuredFieldItem(new StringItem(Name));

        // Parameters in canonical order per RFC 9421 §2.1
        if (Sf) item.Parameters.Add("sf", BooleanItem.True);
        if (Key is not null) item.Parameters.Add("key", new StringItem(Key));
        if (Bs) item.Parameters.Add("bs", BooleanItem.True);
        if (Req) item.Parameters.Add("req", BooleanItem.True);
        if (Tr) item.Parameters.Add("tr", BooleanItem.True);
        if (QueryParamName is not null) item.Parameters.Add("name", new StringItem(QueryParamName));

        return item;
    }

    /// <inheritdoc/>
    public bool Equals(ComponentIdentifier? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name
            && Sf == other.Sf
            && Key == other.Key
            && Bs == other.Bs
            && Req == other.Req
            && Tr == other.Tr
            && QueryParamName == other.QueryParamName;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as ComponentIdentifier);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(Name, Sf, Key, Bs, Req, Tr, QueryParamName);

    /// <inheritdoc/>
    public override string ToString() => Serialize();
}
