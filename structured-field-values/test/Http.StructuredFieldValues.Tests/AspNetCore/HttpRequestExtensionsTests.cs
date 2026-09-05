// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using DamianH.Http.StructuredFieldValues;
using DamianH.Http.StructuredFieldValues.Mapping;
using Microsoft.AspNetCore.Http;
using Shouldly;

namespace DamianH.Http.StructuredFieldValues.AspNetCore;

public class HttpRequestExtensionsTests
{
    private static readonly StructuredFieldMapper<PriorityHeader> PriorityMapper =
        StructuredFieldMapper<PriorityHeader>.Dictionary(b => b
            .Member("u", x => x.Urgency)
            .Member("i", x => x.Incremental));

    private static readonly StructuredFieldMapper<AcceptClientHintHeaderValue> AcceptChMapper =
        StructuredFieldMapper<AcceptClientHintHeaderValue>.List(b => b
            .Elements(x => x.Hints, type: ItemType.Token));

    [Fact]
    public void TryGetHeader_PresentEmptyValues_AreDelegatedToMapper()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["Priority"] = "";
        context.Request.Headers["Accept-CH"] = "";
        var required = StructuredFieldMapper<PriorityHeader>.Dictionary(b =>
            b.Member("u", x => x.Urgency, presence: MappingPresence.Required));
        var item = StructuredFieldMapper<EncodingItem>.Item(b =>
            b.Value(x => x.Encoding, type: ItemType.Token));

        context.Request.TryGetHeader("Priority", PriorityMapper, out var priority).ShouldBeTrue();
        priority.ShouldNotBeNull();
        priority.Urgency.ShouldBeNull();
        context.Request.TryGetHeader("Accept-CH", AcceptChMapper, out var list).ShouldBeTrue();
        list.ShouldNotBeNull();
        list.Hints.ShouldBeEmpty();
        context.Request.TryGetHeader("Priority", required, out _).ShouldBeFalse();
        context.Request.TryGetHeader("Priority", item, out _).ShouldBeFalse();
        context.Request.TryGetHeader("Missing", AcceptChMapper, out _).ShouldBeFalse();
    }

    [Fact]
    public void TryGetHeader_WithValidDictionaryHeader_Success()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["Priority"] = "u=3, i";

        var result = context.Request.TryGetHeader("Priority", PriorityMapper, out var priority);

        result.ShouldBeTrue();
        priority.ShouldNotBeNull();
        priority.Urgency.ShouldBe(3);
        priority.Incremental.ShouldBe(true);
    }

    [Fact]
    public void TryGetHeader_WithMissingHeader_ReturnsFalse()
    {
        var context = new DefaultHttpContext();

        var result = context.Request.TryGetHeader("Priority", PriorityMapper, out var priority);

        result.ShouldBeFalse();
        priority.ShouldBeNull();
    }

    [Fact]
    public void TryGetHeader_WithInvalidHeader_ReturnsFalse()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["Priority"] = "<<<invalid>>>";

        var result = context.Request.TryGetHeader("Priority", PriorityMapper, out var priority);

        result.ShouldBeFalse();
        priority.ShouldBeNull();
    }

    [Fact]
    public void TryGetHeader_WithValidListHeader_Success()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["Accept-CH"] = "Sec-CH-UA, Sec-CH-UA-Platform";

        var result = context.Request.TryGetHeader("Accept-CH", AcceptChMapper, out var header);

        result.ShouldBeTrue();
        header.ShouldNotBeNull();
        header.Hints.Count.ShouldBe(2);
        header.Hints[0].ShouldBe("Sec-CH-UA");
        header.Hints[1].ShouldBe("Sec-CH-UA-Platform");
    }

    [Fact]
    public void TryGetHeader_WithPartialDictionaryHeader_Success()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["Priority"] = "u=5";

        var result = context.Request.TryGetHeader("Priority", PriorityMapper, out var priority);

        result.ShouldBeTrue();
        priority.ShouldNotBeNull();
        priority.Urgency.ShouldBe(5);
        priority.Incremental.ShouldBeNull();
    }

    [Fact]
    public void TryGetHeader_RoundTrip_Success()
    {
        var context = new DefaultHttpContext();
        var original = new PriorityHeader { Urgency = 3, Incremental = true };
        context.Request.Headers["Priority"] = PriorityMapper.Serialize(original);

        var result = context.Request.TryGetHeader("Priority", PriorityMapper, out var parsed);

        result.ShouldBeTrue();
        parsed.ShouldNotBeNull();
        PriorityMapper.Serialize(parsed).ShouldBe(PriorityMapper.Serialize(original));
    }
}
