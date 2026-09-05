// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.StructuredFieldValues.Mapping;

public class ListMapperTests
{
    private static readonly StructuredFieldMapper<AcceptClientHintHeaderValue> AcceptChMapper =
        StructuredFieldMapper<AcceptClientHintHeaderValue>.List(b => b
            .Elements(x => x.Hints, type: ItemType.Token));

    [Fact]
    public void Parse_ValidHeader_ReturnsExpectedValues()
    {
        var header = AcceptChMapper.Parse("Sec-CH-UA, Sec-CH-UA-Platform, Sec-CH-UA-Mobile");

        header.Hints.Count.ShouldBe(3);
        header.Hints[0].ShouldBe("Sec-CH-UA");
        header.Hints[1].ShouldBe("Sec-CH-UA-Platform");
        header.Hints[2].ShouldBe("Sec-CH-UA-Mobile");
    }

    [Fact]
    public void Parse_SingleHint_ReturnsExpectedValue()
    {
        var header = AcceptChMapper.Parse("Sec-CH-UA");

        header.Hints.Count.ShouldBe(1);
        header.Hints[0].ShouldBe("Sec-CH-UA");
    }

    [Fact]
    public void TryParse_ValidInput_ReturnsTrue()
    {
        var result = AcceptChMapper.TryParse("Sec-CH-UA, Sec-CH-UA-Platform", out var header);

        result.ShouldBeTrue();
        header.ShouldNotBeNull();
        header.Hints.Count.ShouldBe(2);
    }

    [Fact]
    public void TryParse_NullInput_ReturnsFalse()
    {
        var result = AcceptChMapper.TryParse(null, out var header);

        result.ShouldBeFalse();
        header.ShouldBeNull();
    }

    [Fact]
    public void TryParse_InvalidInput_ReturnsFalse()
    {
        var result = AcceptChMapper.TryParse("<<<invalid>>>", out var header);

        result.ShouldBeFalse();
        header.ShouldBeNull();
    }

    [Fact]
    public void Serialize_ReturnsExpectedString()
    {
        var header = new AcceptClientHintHeaderValue
        {
            Hints = ["Sec-CH-UA", "Sec-CH-UA-Platform", "Sec-CH-UA-Mobile"]
        };

        AcceptChMapper.Serialize(header).ShouldBe("Sec-CH-UA, Sec-CH-UA-Platform, Sec-CH-UA-Mobile");
    }

    [Fact]
    public void Roundtrip_ParseAndSerialize_ReturnsEquivalentValue()
    {
        var original = "Sec-CH-UA, Sec-CH-UA-Platform";
        var parsed = AcceptChMapper.Parse(original);

        AcceptChMapper.Serialize(parsed).ShouldBe(original);
    }

    [Fact]
    public void Contains_IsCaseInsensitive()
    {
        var header = AcceptChMapper.Parse("Sec-CH-UA, Sec-CH-UA-Platform");

        header.Contains("Sec-CH-UA").ShouldBeTrue();
        header.Contains("sec-ch-ua").ShouldBeTrue();
        header.Contains("Sec-CH-UA-Mobile").ShouldBeFalse();
    }

    [Fact]
    public void Parse_IntegerElements_ReturnsExpectedValues()
    {
        // Test with a list of integers
        var mapper = StructuredFieldMapper<IntListHolder>.List(b => b
            .Elements(x => x.Values));

        var result = mapper.Parse("1, 2, 3");

        result.Values.Count.ShouldBe(3);
        result.Values[0].ShouldBe(1L);
        result.Values[1].ShouldBe(2L);
        result.Values[2].ShouldBe(3L);
    }

    private class IntListHolder
    {
        public IReadOnlyList<long> Values { get; init; } = [];
    }
}
