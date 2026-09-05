// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues.Mapping;

/// <summary>Controls whether a mapped member or parameter must be present.</summary>
public enum MappingPresence
{
    /// <summary>Requires non-nullable value types; reference and nullable value types are optional.</summary>
    Auto,

    /// <summary>Rejects absence on parse and null on serialization.</summary>
    Required,

    /// <summary>
    /// Leaves the property initializer unchanged when absent and omits null on serialization.
    /// Non-nullable value properties do not retain absence and are always serialized.
    /// </summary>
    Optional
}
