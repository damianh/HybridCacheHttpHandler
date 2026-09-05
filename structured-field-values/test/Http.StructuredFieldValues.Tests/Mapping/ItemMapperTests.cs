// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.StructuredFieldValues.Mapping;

public class ItemMapperTests
{
    private static readonly StructuredFieldMapper<EncodingItem> EncodingMapper =
        StructuredFieldMapper<EncodingItem>.Item(b => b
            .Value(x => x.Encoding, type: ItemType.Token)
            .Parameter("q", x => x.Quality));

    [Fact]
    public void Parse_TokenWithQualityParameter_ReturnsExpectedValues()
    {
        var item = EncodingMapper.Parse("gzip;q=0.9");

        item.Encoding.ShouldBe("gzip");
        item.Quality.ShouldBe(0.9m);
    }

    [Fact]
    public void Parse_TokenWithoutParameters_ReturnsExpectedValues()
    {
        var item = EncodingMapper.Parse("gzip");

        item.Encoding.ShouldBe("gzip");
        item.Quality.ShouldBeNull();
    }

    [Fact]
    public void TryParse_ValidInput_ReturnsTrue()
    {
        var result = EncodingMapper.TryParse("gzip;q=0.8", out var item);

        result.ShouldBeTrue();
        item.ShouldNotBeNull();
        item.Encoding.ShouldBe("gzip");
        item.Quality.ShouldBe(0.8m);
    }

    [Fact]
    public void TryParse_NullInput_ReturnsFalse()
    {
        var result = EncodingMapper.TryParse(null, out var item);

        result.ShouldBeFalse();
        item.ShouldBeNull();
    }

    [Fact]
    public void TryParse_InvalidInput_ReturnsFalse()
    {
        var result = EncodingMapper.TryParse("<<<invalid>>>", out var item);

        result.ShouldBeFalse();
        item.ShouldBeNull();
    }

    [Fact]
    public void Serialize_WithParameters_ReturnsExpectedString()
    {
        var item = new EncodingItem { Encoding = "gzip", Quality = 0.9m };

        EncodingMapper.Serialize(item).ShouldBe("gzip;q=0.9");
    }

    [Fact]
    public void Serialize_WithoutOptionalParameter_OmitsParameter()
    {
        var item = new EncodingItem { Encoding = "identity" };

        EncodingMapper.Serialize(item).ShouldBe("identity");
    }

    [Fact]
    public void Roundtrip_ParseAndSerialize_ReturnsEquivalentValue()
    {
        var original = "br;q=0.8";
        var parsed = EncodingMapper.Parse(original);

        EncodingMapper.Serialize(parsed).ShouldBe(original);
    }

    [Fact]
    public void Parse_BooleanItem_ReturnsExpectedValue()
    {
        var mapper = StructuredFieldMapper<BoolItemHolder>.Item(b => b
            .Value(x => x.Flag));

        var result = mapper.Parse("?1");

        result.Flag.ShouldBe(true);
    }

    [Fact]
    public void Parse_IntegerItem_ReturnsExpectedValue()
    {
        var mapper = StructuredFieldMapper<IntItemHolder>.Item(b => b
            .Value(x => x.Count));

        var result = mapper.Parse("42");

        result.Count.ShouldBe(42);
    }

    private class BoolItemHolder
    {
        public bool? Flag { get; init; }
    }

    private class IntItemHolder
    {
        public int? Count { get; init; }
    }
}
