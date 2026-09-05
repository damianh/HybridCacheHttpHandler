// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues.Mapping;

/// <summary>
/// Identifies the RFC 9651 bare item type for a mapped property.
/// </summary>
internal enum ValueKind
{
    /// <summary>Signed integer (maps to <see cref="IntegerItem"/>).</summary>
    Integer,

    /// <summary>Decimal number (maps to <see cref="DecimalItem"/>).</summary>
    Decimal,

    /// <summary>Boolean (maps to <see cref="BooleanItem"/>).</summary>
    Boolean,

    /// <summary>Quoted string (maps to <see cref="StringItem"/>).</summary>
    String,

    /// <summary>Unquoted token (maps to <see cref="TokenItem"/>).</summary>
    Token,

    /// <summary>Base64 byte sequence (maps to <see cref="ByteSequenceItem"/>).</summary>
    ByteSequence,

    /// <summary>Unix seconds (maps to <see cref="DateItem"/>).</summary>
    Date,

    /// <summary>Unicode display string (maps to <see cref="DisplayStringItem"/>).</summary>
    DisplayString,
}
