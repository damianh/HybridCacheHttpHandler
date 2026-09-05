// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using DamianH.Http.StructuredFieldValues;
using DamianH.Http.StructuredFieldValues.Mapping;
using Microsoft.AspNetCore.Http;
using Shouldly;

namespace DamianH.Http.StructuredFieldValues.AspNetCore;

public class HttpResponseExtensionsTests
{
    private static readonly StructuredFieldMapper<PriorityHeader> PriorityMapper =
        StructuredFieldMapper<PriorityHeader>.Dictionary(b => b
            .Member("u", x => x.Urgency)
            .Member("i", x => x.Incremental));

    private static readonly StructuredFieldMapper<AcceptClientHintHeaderValue> AcceptChMapper =
        StructuredFieldMapper<AcceptClientHintHeaderValue>.List(b => b
            .Elements(x => x.Hints, type: ItemType.Token));

    [Fact]
    public void SetHeader_EmptyDictionaryAndList_PreservePresentHeaders()
    {
        var context = new DefaultHttpContext();
        context.Response.SetHeader("Priority", PriorityMapper, new PriorityHeader());
        context.Response.SetHeader("Accept-CH", AcceptChMapper, new AcceptClientHintHeaderValue());

        context.Response.Headers.ContainsKey("Priority").ShouldBeTrue();
        context.Response.Headers["Priority"].ToString().ShouldBe("");
        context.Response.Headers.ContainsKey("Accept-CH").ShouldBeTrue();
        context.Response.Headers["Accept-CH"].ToString().ShouldBe("");
    }

    [Fact]
    public void SetHeader_WithDictionaryType_Success()
    {
        var context = new DefaultHttpContext();
        var priority = new PriorityHeader { Urgency = 5, Incremental = true };

        context.Response.SetHeader("Priority", PriorityMapper, priority);

        context.Response.Headers["Priority"].ToString().ShouldBe("u=5, i");
    }

    [Fact]
    public void SetHeader_WithListType_Success()
    {
        var context = new DefaultHttpContext();
        var header = new AcceptClientHintHeaderValue { Hints = ["Sec-CH-UA", "Sec-CH-UA-Platform"] };

        context.Response.SetHeader("Accept-CH", AcceptChMapper, header);

        context.Response.Headers["Accept-CH"].ToString().ShouldBe("Sec-CH-UA, Sec-CH-UA-Platform");
    }

    [Fact]
    public void SetHeader_OverwritesExistingHeader()
    {
        var context = new DefaultHttpContext();
        context.Response.Headers["Priority"] = "u=1";
        var priority = new PriorityHeader { Urgency = 7 };

        context.Response.SetHeader("Priority", PriorityMapper, priority);

        context.Response.Headers["Priority"].ToString().ShouldBe("u=7");
    }

    [Fact]
    public void SetHeader_WithBooleanFlagOnly_Success()
    {
        var context = new DefaultHttpContext();
        var priority = new PriorityHeader { Incremental = true };

        context.Response.SetHeader("Priority", PriorityMapper, priority);

        context.Response.Headers["Priority"].ToString().ShouldBe("i");
    }

    [Fact]
    public void SetHeader_RoundTrip_Success()
    {
        var context = new DefaultHttpContext();
        var original = new PriorityHeader { Urgency = 3, Incremental = true };

        context.Response.SetHeader("Priority", PriorityMapper, original);

        var requestCtx = new DefaultHttpContext();
        requestCtx.Request.Headers["Priority"] = context.Response.Headers["Priority"].ToString();

        var result = requestCtx.Request.TryGetHeader("Priority", PriorityMapper, out var parsed);

        result.ShouldBeTrue();
        parsed.ShouldNotBeNull();
        parsed.Urgency.ShouldBe(3);
        parsed.Incremental.ShouldBe(true);
    }

    [Fact]
    public void SetHeader_WithListType_MultipleHints_Success()
    {
        var context = new DefaultHttpContext();
        var header = new AcceptClientHintHeaderValue { Hints = ["Sec-CH-UA", "Sec-CH-UA-Platform", "Sec-CH-UA-Mobile"] };

        context.Response.SetHeader("Accept-CH", AcceptChMapper, header);

        context.Response.Headers["Accept-CH"].ToString().ShouldBe("Sec-CH-UA, Sec-CH-UA-Platform, Sec-CH-UA-Mobile");
    }
}
