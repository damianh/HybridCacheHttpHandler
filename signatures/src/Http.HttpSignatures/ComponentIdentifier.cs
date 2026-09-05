// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using DamianH.Http.StructuredFieldValues;
using System.Collections.ObjectModel;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Represents a component identifier per RFC 9421 §2.
/// Combines a component name with optional parameters (sf, key, bs, req, tr, name).
/// Component identifiers appear in the covered components list of a signature.
/// </summary>
public sealed class ComponentIdentifier : IEquatable<ComponentIdentifier>
{
    /// <summary>
    /// The exact wire parameters as parsed, preserving order and any unknown parameters,
    /// for full-fidelity round-trip serialization. Null for locally constructed instances,
    /// which instead serialize the typed properties in RFC 9421 canonical order.
    /// </summary>
    private readonly Parameters? _wireParameters;

    /// <summary>
    /// Lazily computed, immutable snapshot of the parameter list, in the order described by
    /// <see cref="Parameters"/>. <see cref="ComponentIdentifier"/> is immutable after
    /// construction, so this is computed at most once and reused by <see cref="Parameters"/>,
    /// <see cref="ToStructuredFieldItem"/>, <see cref="Equals(ComponentIdentifier?)"/>, and
    /// <see cref="GetHashCode"/>.
    /// </summary>
    private ReadOnlyCollection<KeyValuePair<string, BareItem>>? _parameterList;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComponentIdentifier"/> class.
    /// </summary>
    /// <param name="name">The component name (e.g., "@method", "content-type").</param>
    public ComponentIdentifier(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name.ToLowerInvariant();
        _wireParameters = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ComponentIdentifier"/> class from parsed wire data,
    /// preserving the exact parameter order (including unknown parameters) for round-trip fidelity.
    /// </summary>
    /// <param name="wireName">The component name exactly as it appeared on the wire (already validated as lowercase).</param>
    /// <param name="wireParameters">The parsed parameters, copied defensively to preserve order.</param>
    private ComponentIdentifier(string wireName, Parameters wireParameters)
    {
        Name = wireName;
        _wireParameters = new Parameters(wireParameters);
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
    /// Gets a value indicating whether the component is taken from trailers instead of headers.
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
    /// Gets a read-only, defensively-copied view of every parameter on this component identifier,
    /// including parameters not represented by a typed property above. For a locally constructed
    /// instance this reflects the known typed properties in RFC 9421 canonical order; for a parsed
    /// instance it reflects the exact wire order, including any unknown/extension parameters.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, BareItem>> Parameters =>
        LazyInitializer.EnsureInitialized(ref _parameterList, ComputeParameterList);

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

    /// <summary>
    /// Creates a <see cref="ComponentIdentifier"/> from a parsed wire component identifier item.
    /// Validates that the name is already lowercase and that reserved parameters have the expected
    /// Structured Field types, without discarding unknown parameters.
    /// </summary>
    /// <param name="wireName">The component name as it appeared on the wire.</param>
    /// <param name="wireParameters">The parameters attached to the wire item.</param>
    /// <returns>The parsed <see cref="ComponentIdentifier"/>.</returns>
    /// <exception cref="FormatException">
    /// Thrown when the name is not lowercase, or a reserved parameter has an unexpected type.
    /// </exception>
    internal static ComponentIdentifier FromWire(string wireName, Parameters wireParameters)
    {
        ArgumentException.ThrowIfNullOrEmpty(wireName);
        ArgumentNullException.ThrowIfNull(wireParameters);

        if (wireName != wireName.ToLowerInvariant())
        {
            throw new FormatException(
                $"Component identifier name '{wireName}' is not valid: RFC 9421 §2.1 requires component names to be lowercase.");
        }

        var sf = ParseFlag(wireParameters, "sf");
        var bs = ParseFlag(wireParameters, "bs");
        var req = ParseFlag(wireParameters, "req");
        var tr = ParseFlag(wireParameters, "tr");

        string? key = null;
        if (wireParameters.TryGetValue("key", out var keyValue))
        {
            if (keyValue is not StringItem keyString)
                throw new FormatException("Component identifier 'key' parameter must be an SF String.");
            key = keyString.StringValue;
        }

        string? queryParamName = null;
        if (wireParameters.TryGetValue("name", out var nameValue))
        {
            if (nameValue is not StringItem nameString)
                throw new FormatException("Component identifier 'name' parameter must be an SF String.");
            queryParamName = nameString.StringValue;
        }

        return new ComponentIdentifier(wireName, wireParameters)
        {
            Sf = sf,
            Key = key,
            Bs = bs,
            Req = req,
            Tr = tr,
            QueryParamName = queryParamName,
        };
    }

    private static bool ParseFlag(Parameters parameters, string key)
    {
        if (!parameters.TryGetValue(key, out var value))
            return false;

        if (value is not BooleanItem booleanItem)
            throw new FormatException($"Component identifier '{key}' parameter must be an SF Boolean.");

        return booleanItem.BooleanValue;
    }

    internal StructuredFieldItem ToStructuredFieldItem()
    {
        var item = new StructuredFieldItem(new StringItem(Name));

        // Reuses the cached parameter list, which already reflects either the exact wire
        // order (including unknown parameters) or the RFC 9421 §2.1 canonical order for a
        // locally constructed instance. The item's Parameters is a fresh, owned collection
        // (see StructuredFieldItem), so mutating it here never aliases the cached snapshot.
        foreach (var kvp in Parameters)
        {
            item.Parameters.Add(kvp.Key, kvp.Value);
        }

        return item;
    }

    /// <summary>
    /// Computes the immutable, order-preserving parameter snapshot backing
    /// <see cref="Parameters"/>. Called at most once per instance and cached, since
    /// <see cref="ComponentIdentifier"/> is immutable after construction.
    /// </summary>
    private ReadOnlyCollection<KeyValuePair<string, BareItem>> ComputeParameterList()
    {
        if (_wireParameters is not null)
        {
            // Full-fidelity round trip: reproduce the exact wire parameter order, including
            // any parameters not represented by a typed property (e.g. future extensions).
            return Array.AsReadOnly(_wireParameters.ToArray());
        }

        // Locally constructed instance: RFC 9421 §2.1 canonical parameter order.
        List<KeyValuePair<string, BareItem>> list = [];
        if (Sf) list.Add(new("sf", BooleanItem.True));
        if (Key is not null) list.Add(new("key", new StringItem(Key)));
        if (Bs) list.Add(new("bs", BooleanItem.True));
        if (Req) list.Add(new("req", BooleanItem.True));
        if (Tr) list.Add(new("tr", BooleanItem.True));
        if (QueryParamName is not null) list.Add(new("name", new StringItem(QueryParamName)));

        return Array.AsReadOnly(list.ToArray());
    }

    /// <inheritdoc/>
    public bool Equals(ComponentIdentifier? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (Name != other.Name) return false;

        // Full parameter identity, independent of serialization order (RFC 9421 §2.5: duplicate
        // detection and required-component checks must not depend on wire parameter order).
        var mine = Parameters;
        var theirs = other.Parameters;
        if (mine.Count != theirs.Count) return false;

        var theirsByKey = new Dictionary<string, BareItem>(theirs.Count, StringComparer.Ordinal);
        foreach (var kvp in theirs)
        {
            theirsByKey[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in mine)
        {
            if (!theirsByKey.TryGetValue(kvp.Key, out var otherValue) || !kvp.Value.Equals(otherValue))
                return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as ComponentIdentifier);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        // Order-independent: combine per-parameter hashes with XOR so identity matches Equals.
        var combinedParameters = 0;
        foreach (var kvp in Parameters)
        {
            combinedParameters ^= HashCode.Combine(kvp.Key, kvp.Value);
        }

        return HashCode.Combine(Name, combinedParameters);
    }

    /// <inheritdoc/>
    public override string ToString() => Serialize();
}
