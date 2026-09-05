using DamianH.Http.StructuredFieldValues.Mapping;
using Shouldly;

namespace DamianH.Http.StructuredFieldValues.HttpClient;

public class HttpRequestMessageExtensionsTests
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
        using var request = new HttpRequestMessage();
        request.SetHeader("Priority", PriorityMapper, new PriorityHeader());
        request.SetHeader("Accept-CH", AcceptChMapper, new AcceptClientHintHeaderValue());
        var required = StructuredFieldMapper<PriorityHeader>.Dictionary(b =>
            b.Member("u", x => x.Urgency, presence: MappingPresence.Required));
        var item = StructuredFieldMapper<EncodingItem>.Item(b =>
            b.Value(x => x.Encoding, type: ItemType.Token));

        request.TryGetHeader("Priority", PriorityMapper, out var priority).ShouldBeTrue();
        priority.ShouldNotBeNull();
        priority.Urgency.ShouldBeNull();
        request.TryGetHeader("Accept-CH", AcceptChMapper, out var list).ShouldBeTrue();
        list.ShouldNotBeNull();
        list.Hints.ShouldBeEmpty();
        request.TryGetHeader("Priority", required, out _).ShouldBeFalse();
        request.TryGetHeader("Priority", item, out _).ShouldBeFalse();
        request.TryGetHeader("Missing", AcceptChMapper, out _).ShouldBeFalse();
    }

    [Fact]
    public void TryGetHeader_WithValidDictionaryHeader_Success()
    {
        var request = new HttpRequestMessage();
        request.Headers.TryAddWithoutValidation("Priority", "u=3, i");

        var result = request.TryGetHeader("Priority", PriorityMapper, out var priority);

        result.ShouldBeTrue();
        priority.ShouldNotBeNull();
        priority.Urgency.ShouldBe(3);
        priority.Incremental.ShouldBe(true);
    }

    [Fact]
    public void TryGetHeader_WithMissingHeader_ReturnsFalse()
    {
        var request = new HttpRequestMessage();

        var result = request.TryGetHeader("Priority", PriorityMapper, out var priority);

        result.ShouldBeFalse();
        priority.ShouldBeNull();
    }

    [Fact]
    public void TryGetHeader_WithInvalidHeader_ReturnsFalse()
    {
        var request = new HttpRequestMessage();
        request.Headers.TryAddWithoutValidation("Priority", "<<<invalid>>>");

        var result = request.TryGetHeader("Priority", PriorityMapper, out var priority);

        result.ShouldBeFalse();
        priority.ShouldBeNull();
    }

    [Fact]
    public void TryGetHeader_WithValidListHeader_Success()
    {
        var request = new HttpRequestMessage();
        request.Headers.TryAddWithoutValidation("Accept-CH", "Sec-CH-UA, Sec-CH-UA-Platform");

        var result = request.TryGetHeader("Accept-CH", AcceptChMapper, out var header);

        result.ShouldBeTrue();
        header.ShouldNotBeNull();
        header.Hints.Count.ShouldBe(2);
        header.Hints[0].ShouldBe("Sec-CH-UA");
        header.Hints[1].ShouldBe("Sec-CH-UA-Platform");
    }

    [Fact]
    public void SetHeader_WithDictionaryType_Success()
    {
        var request = new HttpRequestMessage();
        var priority = new PriorityHeader { Urgency = 5, Incremental = true };

        request.SetHeader("Priority", PriorityMapper, priority);

        request.Headers.GetValues("Priority").Single().ShouldBe("u=5, i");
    }

    [Fact]
    public void SetHeader_WithListType_Success()
    {
        var request = new HttpRequestMessage();
        var header = new AcceptClientHintHeaderValue { Hints = ["Sec-CH-UA", "Sec-CH-UA-Platform"] };

        request.SetHeader("Accept-CH", AcceptChMapper, header);

        request.Headers.GetValues("Accept-CH").Single().ShouldBe("Sec-CH-UA, Sec-CH-UA-Platform");
    }

    [Fact]
    public void SetHeader_RoundTrip_Success()
    {
        var request = new HttpRequestMessage();
        var original = new PriorityHeader { Urgency = 3, Incremental = true };

        request.SetHeader("Priority", PriorityMapper, original);
        var result = request.TryGetHeader("Priority", PriorityMapper, out var parsed);

        result.ShouldBeTrue();
        parsed.ShouldNotBeNull();
        parsed.Urgency.ShouldBe(3);
        parsed.Incremental.ShouldBe(true);
    }
}
