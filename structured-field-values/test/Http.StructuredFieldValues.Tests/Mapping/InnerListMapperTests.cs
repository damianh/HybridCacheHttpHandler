// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.StructuredFieldValues.Mapping;

public class InnerListMapperTests
{
    private class FeaturePolicy
    {
        public IReadOnlyList<string>? Camera { get; init; }
        public IReadOnlyList<string>? Microphone { get; init; }
    }

    private static readonly StructuredFieldMapper<FeaturePolicy> FpMapper =
        StructuredFieldMapper<FeaturePolicy>.Dictionary(b => b
            .InnerList("camera", x => x.Camera, type: ItemType.Token)
            .InnerList("microphone", x => x.Microphone, type: ItemType.Token));

    [Fact]
    public void Parse_DictionaryWithTokenInnerLists_ReturnsExpectedValues()
    {
        var fp = FpMapper.Parse("camera=(self), microphone=(self https://example.com)");

        fp.Camera.ShouldNotBeNull();
        fp.Camera!.Count.ShouldBe(1);
        fp.Camera![0].ShouldBe("self");

        fp.Microphone.ShouldNotBeNull();
        fp.Microphone!.Count.ShouldBe(2);
        fp.Microphone![0].ShouldBe("self");
        fp.Microphone![1].ShouldBe("https://example.com");
    }

    [Fact]
    public void Parse_MissingOptionalInnerList_ReturnsNull()
    {
        var fp = FpMapper.Parse("camera=(self)");

        fp.Camera.ShouldNotBeNull();
        fp.Microphone.ShouldBeNull();
    }

    [Fact]
    public void Serialize_TokenInnerLists_ReturnsExpectedString()
    {
        var fp = new FeaturePolicy
        {
            Camera = ["self"],
            Microphone = ["self", "https://example.com"]
        };

        FpMapper.Serialize(fp).ShouldBe("camera=(self), microphone=(self https://example.com)");
    }

    [Fact]
    public void Roundtrip_InnerList_ReturnsEquivalentValue()
    {
        var original = "camera=(self https://example.com)";
        var parsed = FpMapper.Parse(original);

        FpMapper.Serialize(parsed).ShouldBe(original);
    }

    [Fact]
    public void Parse_EmptyInnerList_ReturnsEmptyCollection()
    {
        var fp = FpMapper.Parse("camera=()");

        fp.Camera.ShouldNotBeNull();
        fp.Camera!.Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_IntegerInnerList_ReturnsExpectedValues()
    {
        var mapper = StructuredFieldMapper<IntListDict>.Dictionary(b => b
            .InnerList<long>("vals", x => x.Values));

        var result = mapper.Parse("vals=(1 2 3)");

        result.Values.ShouldNotBeNull();
        result.Values!.Count.ShouldBe(3);
        result.Values![0].ShouldBe(1L);
        result.Values![1].ShouldBe(2L);
        result.Values![2].ShouldBe(3L);
    }

    private class IntListDict
    {
        public IReadOnlyList<long>? Values { get; init; }
    }
}
