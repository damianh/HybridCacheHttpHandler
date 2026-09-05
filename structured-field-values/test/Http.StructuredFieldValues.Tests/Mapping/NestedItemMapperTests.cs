// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.StructuredFieldValues.Mapping;

public class NestedItemMapperTests
{
    private static readonly StructuredFieldMapper<EncodingItem> EncodingItemMapper =
        StructuredFieldMapper<EncodingItem>.Item(b => b
            .Value(x => x.Encoding, type: ItemType.Token)
            .Parameter("q", x => x.Quality));

    private class AcceptEncodingHeader
    {
        public IReadOnlyList<EncodingItem> Encodings { get; init; } = [];
    }

    private static readonly StructuredFieldMapper<AcceptEncodingHeader> AcceptEncMapper =
        StructuredFieldMapper<AcceptEncodingHeader>.List(b => b
            .Elements(x => x.Encodings, EncodingItemMapper));

    [Fact]
    public void Parse_ListOfNestedItems_ReturnsExpectedValues()
    {
        var header = AcceptEncMapper.Parse("gzip;q=0.9, br;q=0.8, identity");

        header.Encodings.Count.ShouldBe(3);
        header.Encodings[0].Encoding.ShouldBe("gzip");
        header.Encodings[0].Quality.ShouldBe(0.9m);
        header.Encodings[1].Encoding.ShouldBe("br");
        header.Encodings[1].Quality.ShouldBe(0.8m);
        header.Encodings[2].Encoding.ShouldBe("identity");
        header.Encodings[2].Quality.ShouldBeNull();
    }

    [Fact]
    public void Serialize_ListOfNestedItems_ReturnsExpectedString()
    {
        var header = new AcceptEncodingHeader
        {
            Encodings =
            [
                new EncodingItem { Encoding = "gzip", Quality = 0.9m },
                new EncodingItem { Encoding = "br", Quality = 0.8m },
                new EncodingItem { Encoding = "identity" }
            ]
        };

        AcceptEncMapper.Serialize(header).ShouldBe("gzip;q=0.9, br;q=0.8, identity");
    }

    [Fact]
    public void Roundtrip_ListOfNestedItems_ReturnsEquivalentValue()
    {
        var original = "gzip;q=0.9, br;q=0.8";
        var parsed = AcceptEncMapper.Parse(original);

        AcceptEncMapper.Serialize(parsed).ShouldBe(original);
    }

    private class DictWithNestedItems
    {
        public IReadOnlyList<EncodingItem>? Encodings { get; init; }
    }

    private static readonly StructuredFieldMapper<DictWithNestedItems> DictWithNestedMapper =
        StructuredFieldMapper<DictWithNestedItems>.Dictionary(b => b
            .InnerList("enc", x => x.Encodings, EncodingItemMapper));

    [Fact]
    public void Parse_DictionaryWithNestedItemInnerList_ReturnsExpectedValues()
    {
        var result = DictWithNestedMapper.Parse("enc=(gzip;q=0.9 br;q=0.8)");

        result.Encodings.ShouldNotBeNull();
        result.Encodings!.Count.ShouldBe(2);
        result.Encodings![0].Encoding.ShouldBe("gzip");
        result.Encodings![0].Quality.ShouldBe(0.9m);
        result.Encodings![1].Encoding.ShouldBe("br");
        result.Encodings![1].Quality.ShouldBe(0.8m);
    }

    [Fact]
    public void Serialize_DictionaryWithNestedItemInnerList_ReturnsExpectedString()
    {
        var value = new DictWithNestedItems
        {
            Encodings =
            [
                new EncodingItem { Encoding = "gzip", Quality = 0.9m },
                new EncodingItem { Encoding = "br", Quality = 0.8m }
            ]
        };

        DictWithNestedMapper.Serialize(value).ShouldBe("enc=(gzip;q=0.9 br;q=0.8)");
    }
}
