# DamianH.Http.StructuredFieldValues

RFC 9651 parser, serializer, and POCO mapper for HTTP Structured Field Values,
including the six original RFC 8941 types plus Dates and Display Strings.

Use this library only for fields defined as Structured Fields, such as `Priority`
and `Accept-CH`, or custom fields with a declared structured type. `Cache-Control`
is **not** a Structured Field; use `System.Net.Http.Headers.CacheControlHeaderValue`
for its grammar.

## Table of Contents

- [Installation](#installation)
- [Quick Start](#quick-start)
  - [Parsing](#parsing)
  - [Serializing](#serializing)
  - [POCO Mapping](#poco-mapping)
- [API Reference](#api-reference)
  - [StructuredFieldParser](#structuredfieldparser)
  - [StructuredFieldSerializer](#structuredfieldserializer)
  - [StructuredFieldMapper\<T\>](#structuredfieldmappert)
  - [Item Types](#item-types)
  - [Collection Types](#collection-types)
  - [DictionaryBuilder\<T\>](#dictionarybuildert)
  - [ListBuilder\<T\>](#listbuildert)
  - [ItemBuilder\<T\>](#itembuildert)
- [Type Mapping](#type-mapping)
- [Ownership and Equality](#ownership-and-equality)
- [Breaking Changes](#breaking-changes)
- [Samples](#samples)

## Installation

```bash
dotnet add package DamianH.Http.StructuredFieldValues
```

## Quick Start

### Parsing

```csharp
using DamianH.Http.StructuredFieldValues;

// Parse a Priority structured dictionary
StructuredFieldDictionary dict = StructuredFieldParser.ParseDictionary("u=3, i");
// dict["u"].Item.Value is IntegerItem(3)
// dict["i"].Item.Value is BooleanItem(true)  (bare key = ?1)

// Parse a structured list
StructuredFieldList list = StructuredFieldParser.ParseList("a, b, c");

// Parse a single item
StructuredFieldItem item = StructuredFieldParser.ParseItem("42");

var eventTime = StructuredFieldParser.ParseItem("@1659578233");
// ((DateItem)eventTime.Value).UnixSeconds == 1659578233

var label = StructuredFieldParser.ParseItem("%\"caf%c3%a9\"");
// label.Value is DisplayStringItem, containing Unicode text
```

### Serializing

```csharp
var dict = new StructuredFieldDictionary
{
    ["u"] = StructuredFieldMember.FromItem(new IntegerItem(3)),
    ["i"] = StructuredFieldMember.FromItem(BooleanItem.True)
};

string header = StructuredFieldSerializer.SerializeDictionary(dict);
// "u=3, i"

var item = new StructuredFieldItem(new TokenItem("gzip"));
item.Parameters.Add("enabled", BooleanItem.True);
string member = StructuredFieldSerializer.SerializeItem(item);
// "gzip;enabled"
```

**`ToString()` is diagnostic-only, not wire output.** Always use
`StructuredFieldSerializer` or the mapper's `Serialize` method when writing
headers. Diagnostic strings need not be quoted, complete, canonical, or
round-trippable.

### POCO Mapping

The mapper converts structured values to and from plain C# objects. Mappers
snapshot their configuration and are reusable across threads; store them in
`static readonly` fields. Mapped models must be classes with a public
parameterless constructor and directly accessible readable/writable properties
(including `init` properties). Concurrent mutation of the same model is not supported.

```csharp
// Define a POCO
public class PriorityHeader
{
    public int? Urgency { get; init; }
    public bool? Incremental { get; init; }

    // Define the mapper once, store statically
    public static readonly StructuredFieldMapper<PriorityHeader> Mapper =
        StructuredFieldMapper<PriorityHeader>.Dictionary(b => b
            .Member("u", x => x.Urgency)
            .Member("i", x => x.Incremental));
}

// Parse
var priority = PriorityHeader.Mapper.Parse("u=3, i");
// priority.Urgency == 3, priority.Incremental == true

// Serialize
string header = PriorityHeader.Mapper.Serialize(new PriorityHeader { Urgency = 3, Incremental = true });
// "u=3, i"

// Try-parse (returns false for malformed input instead of throwing)
if (PriorityHeader.Mapper.TryParse(request.Headers["Priority"], out var p))
{
    // use p
}
```

## API Reference

### StructuredFieldParser

Static class for parsing RFC 9651 structured field values.

| Method | Returns | Description |
|--------|---------|-------------|
| `ParseItem(string input)` | `StructuredFieldItem` | Parses an item with optional parameters |
| `ParseBareItem(string input)` | `BareItem` | Parses a scalar, rejecting parameters |
| `ParseList(string input)` | `StructuredFieldList` | Parses a list of items and/or inner lists (RFC 8941 §4.2.1) |
| `ParseDictionary(string input)` | `StructuredFieldDictionary` | Parses a dictionary of key→member pairs (RFC 8941 §4.2.2) |

All methods throw `ArgumentNullException` for null input and `StructuredFieldParseException` for malformed input.
Empty strings are valid lists/dictionaries but not items. Duplicate dictionary
and parameter keys keep their original position and use the last parsed value.
Implicit Boolean true parameters (`;flag`) and explicit ones (`;flag=?1`) produce
the same bare Boolean value.

### StructuredFieldSerializer

Static class for serializing structured field values to their canonical wire form.

| Method | Returns | Description |
|--------|---------|-------------|
| `SerializeItem(StructuredFieldItem item)` | `string` | Serializes an item with parameters (RFC 8941 §4.1.3) |
| `SerializeList(StructuredFieldList list)` | `string` | Serializes a list (RFC 8941 §4.1.1) |
| `SerializeDictionary(StructuredFieldDictionary dictionary)` | `string` | Serializes a dictionary (RFC 8941 §4.1.2) |
| `SerializeBareItem(BareItem value)` | `string` | Serializes a scalar without parameters |
| `SerializeInnerList(InnerList list)` | `string` | Serializes a parenthesized inner list with its parameters |
| `SerializeMember(StructuredFieldMember member)` | `string` | Serializes an item or inner list without a dictionary key |

All methods use the same wire writer. Parameters and dictionary entries retain
insertion order, and Boolean true uses shorthand where permitted.

### StructuredFieldMapper\<T\>

A cached, reusable mapper that converts a structured field value to and from a
POCO of type `T`, constrained to `class, new()`.

**Factory methods** (choose based on the RFC 8941 field type):

| Factory | When to use |
|---------|------------|
| `StructuredFieldMapper<T>.Dictionary(Action<DictionaryBuilder<T>> configure)` | Field is a Dictionary (e.g. `Priority`) |
| `StructuredFieldMapper<T>.List(Action<ListBuilder<T>> configure)` | Field is an RFC 8941 List |
| `StructuredFieldMapper<T>.Item(Action<ItemBuilder<T>> configure)` | Field is an RFC 8941 Item |

**Instance methods:**

| Method | Description |
|--------|-------------|
| `T Parse(string input)` | Parses the header value. Throws `StructuredFieldParseException` on failure. |
| `bool TryParse(string? input, out T? result)` | Returns false instead of throwing for missing or malformed input. |
| `string Serialize(T value)` | Serializes the POCO to its canonical RFC 8941 string. |

`TryParse` agrees with `Parse` for valid input: an empty list or optional-only
dictionary succeeds; an empty dictionary with required mappings fails. `null`
means missing input and returns false. Malformed values and mapping mismatches
return false, but configuration and user property-accessor exceptions are not
silently swallowed.

The mapper is a **projection**, not a lossless document editor: unknown
members/parameters are ignored and are not retained on serialization. Use the
object model when preserving all fields and their order matters.

### Item Types

All scalar types extend immutable `BareItem`; they do not carry parameters.
`StructuredFieldItem` wraps a bare value in its `.Value` property and owns one
mutable `.Parameters` collection.

| Type | CLR value | Wire format | Example |
|------|-----------|---------------------|---------|
| `IntegerItem` | `long` via `.LongValue` | Signed integer | `42`, `-1` |
| `DecimalItem` | `decimal` via `.DecimalValue` | Up to 12 integer digits and 3 fractional digits | `3.14` |
| `StringItem` | `string` via `.StringValue` | Quoted string | `"hello"` |
| `TokenItem` | `string` via `.TokenValue` | Unquoted token | `gzip`, `*` |
| `ByteSequenceItem` | Read-only `.Bytes`; copy out with `.ToArray()` | `:base64:` | `:aGVsbG8=:` |
| `BooleanItem` | `bool` via `.BooleanValue` | `?0` / `?1` | `?1` |
| `DateItem` | Unix seconds as `long` via `.UnixSeconds` | `@` followed by an integer | `@1659578233` |
| `DisplayStringItem` | Unicode `string` via `.StringValue` | UTF-8 percent encoding inside `%"..."` | `%"caf%c3%a9"` |

Decimals range from `-999999999999.999` through `999999999999.999`.
Construction rejects excess fractional precision rather than silently rounding.
Dates support the full signed 15-digit integer range, not just the narrower
`DateTimeOffset` range. Display Strings reject malformed Unicode rather than
silently replacing it; ordinary Strings remain printable ASCII-only.

### Collection Types

| Type | Description |
|------|-------------|
| `StructuredFieldList` | Ordered list of `StructuredFieldMember` (item or inner list). Supports `Add`, `AddRange`, count, and indexer access. |
| `StructuredFieldDictionary` | Ordered dictionary of `string` to `StructuredFieldMember`. Supports enumeration and indexer access. |
| `InnerList` | A parenthesised list of `StructuredFieldItem` entries, with its own `Parameters`. |
| `Parameters` | Ordered map of valid keys to non-null `BareItem` values. A present flag is `BooleanItem.True`; absence means no key. |
| `StructuredFieldMember` | Shared item-or-inner-list wrapper. Its parameters belong to the contained node, not a separate member-level collection. |

### DictionaryBuilder\<T\>

Configures mappings from an RFC 8941 Dictionary to POCO properties.

| Method | Description |
|--------|-------------|
| `.Member(key, x => x.Prop, type: ..., presence: ...)` | Maps a primitive property; type and presence arguments are optional. |
| `.InnerList(key, x => x.Prop, type: ..., presence: ...)` | Maps an `IReadOnlyList<TElement>?` of primitive elements. Optional by default. |
| `.InnerList(key, x => x.Prop, elementMapper)` | Maps a key to an `IReadOnlyList<TElement>?` property where each element is mapped by a nested `StructuredFieldMapper<TElement>`. |

Nested element mappers must be created with `Item`; passing a list or
dictionary mapper fails during configuration.

### ListBuilder\<T\>

Configures mappings from an RFC 8941 List to a POCO.

| Method | Description |
|--------|-------------|
| `.Elements(x => x.Prop, type: ...)` | Maps primitive elements to an `IReadOnlyList<TElement>` property; the wire type is optional. |
| `.Elements(x => x.Prop, elementMapper)` | Maps items with parameters using a nested item mapper. |

A null top-level collection serializes as an empty list. Null elements are
rejected, not dropped. This mapper does not support inner lists as list members.

### ItemBuilder\<T\>

Configures mappings from an RFC 8941 Item to a POCO.

| Method | Description |
|--------|-------------|
| `.Value(x => x.Prop, type: ...)` | Maps the required bare item value; the wire type is optional. |
| `.Parameter(paramKey, x => x.Prop, type: ..., presence: ...)` | Maps a parameter; wire type and presence are optional. |

Exactly one value mapping is required. A null item value throws on serialization,
even if its CLR property is nullable. Boolean flags require an explicit Boolean
value mapping; there are no placeholder values.

## Type Mapping

The mapper infers types from CLR property types. An explicit `type: ItemType.X`
selects another compatible wire representation; incompatible combinations fail
at mapper construction.

| CLR Type | Structured Type | Notes |
|----------|--------------|-------|
| `int`, `long` | Integer | Range: −999,999,999,999,999 to 999,999,999,999,999 |
| `decimal` | Decimal | Up to 12 integer digits and 3 fractional places |
| `bool` | Boolean | `?1` / `?0`; bare key in dictionaries = `?1` |
| `string` | String | Override with `type: ItemType.Token` or `ItemType.DisplayString` |
| `byte[]` | Byte Sequence | `:base64:` encoding |
| `long` with `type: ItemType.Date` | Date | Full-range Unix seconds; default `long` mapping remains Integer |

Presence is configured independently using `MappingPresence` from
`DamianH.Http.StructuredFieldValues.Mapping`:

| Presence | Behavior |
|----------|----------|
| `Auto` (default) | Non-nullable value properties are required; nullable value and all reference properties are optional. Inner lists are optional. |
| `Required` | Missing members/parameters fail parsing; null properties fail serialization. |
| `Optional` | Missing members/parameters leave property initializers unchanged; null properties are omitted. |

C# reference-type nullable annotations do not change these defaults. An optional
non-nullable value property cannot distinguish absence from its default value.

```csharp
using DamianH.Http.StructuredFieldValues;
using DamianH.Http.StructuredFieldValues.Mapping;

public class EventMetadata
{
    public string Label { get; init; } = "";
    public string? Kind { get; init; }
    public long Timestamp { get; init; }

    public static readonly StructuredFieldMapper<EventMetadata> Mapper =
        StructuredFieldMapper<EventMetadata>.Dictionary(b => b
            .Member("label", x => x.Label,
                type: ItemType.DisplayString, presence: MappingPresence.Required)
            .Member("kind", x => x.Kind, type: ItemType.Token)
            .Member("at", x => x.Timestamp, type: ItemType.Date));
}
```

## Ownership and Equality

Bare values have value equality and no mutable state. Byte sequences copy
incoming arrays, and copy-out operations cannot change the stored bytes or hash.
Boolean singletons are safe to reuse as bare values.

Items, inner lists, parameters, and collections are mutable and use reference
equality. Item/inner-list constructors copy supplied parameter collections.
Mutate parameters through their owning node; a `StructuredFieldMember` does not
introduce a second collection. Explicitly sharing a mutable item between lists
is possible, so modifying that item is visible to both lists.

## Breaking Changes

- Scalar classes now derive from `BareItem`, not `StructuredFieldItem`. Wrap
  values in `new StructuredFieldItem(bareValue)` before attaching parameters;
  inspect parsed scalars through `item.Value`.
- Replace `ListMember` and `DictionaryMember` with `StructuredFieldMember`.
  Supply parameters to the item or inner-list constructor, not `FromItem`.
- Replace null-valued parameters with `BooleanItem.True`. `TryGetValue` returning
  true always yields a non-null bare value.
- Replace mutable byte-array access with `.Bytes` for reading or `.ToArray()`
  for a copy.
- Replace `TokenMember`, `TokenValue`, `TokenParameter`, `TokenElements`, and
  `TokenInnerList` with the corresponding ordinary method and
  `type: ItemType.Token`.
- Mapper models and nested models must be classes; propagate `class, new()`
  constraints through generic helpers. Invalid mappings fail at construction.
- Empty list/dictionary input can now succeed in `TryParse`. HTTP helpers
  distinguish a missing header from a present empty value.
- Explicit Boolean true parameters serialize in shorthand. `ToString()` remains
  diagnostic-only and must not be used as a substitute for serialization.

## Samples

- [`samples/HttpClientSample`](samples/HttpClientSample) — `HttpClient` integration for `Priority` (RFC 9218), with helpers for reading and writing structured headers on `HttpRequestMessage` and `HttpResponseMessage`.

- [`samples/AspNetCoreSample`](samples/AspNetCoreSample) — ASP.NET Core integration for `Priority`, `Accept-CH`, and a custom token-list field.

> **Note**: The sample projects target .NET 10 with `LangVersion=preview` and use C# 14 extension declaration syntax.
