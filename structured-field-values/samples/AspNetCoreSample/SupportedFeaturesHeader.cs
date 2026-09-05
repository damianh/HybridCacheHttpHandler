using DamianH.Http.StructuredFieldValues;

namespace AspNetCoreSample;

/// <summary>
/// Represents an X-Supported-Features header as an RFC 8941 Structured List of tokens.
/// </summary>
public class SupportedFeaturesHeader
{
    /// <summary>Gets the supported feature tokens.</summary>
    public IReadOnlyList<string> Features { get; init; } = [];

    /// <summary>Mapper for parsing and serializing X-Supported-Features headers.</summary>
    public static readonly StructuredFieldMapper<SupportedFeaturesHeader> Mapper =
        StructuredFieldMapper<SupportedFeaturesHeader>.List(b => b
            .Elements(x => x.Features, type: ItemType.Token));
}
