// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.StructuredFieldValues;

public class ParserListTests
{
    [Fact]
    public void ParseList_Empty_Success()
    {
        // Arrange & Act
        var list = StructuredFieldParser.ParseList("");

        // Assert
        list.Count.ShouldBe(0);
    }

    [Fact]
    public void ParseList_SingleItem_Success()
    {
        // Arrange & Act
        var list = StructuredFieldParser.ParseList("foo");

        // Assert
        list.Count.ShouldBe(1);
        list[0].IsItem.ShouldBeTrue();
        list[0].Item.Value.ShouldBeOfType<TokenItem>();
    }

    [Fact]
    public void ParseList_MultipleItems_Success()
    {
        // Arrange & Act
        var list = StructuredFieldParser.ParseList("foo, bar, baz");

        // Assert
        list.Count.ShouldBe(3);
        ((TokenItem)list[0].Item.Value).TokenValue.ShouldBe("foo");
        ((TokenItem)list[1].Item.Value).TokenValue.ShouldBe("bar");
        ((TokenItem)list[2].Item.Value).TokenValue.ShouldBe("baz");
    }

    [Fact]
    public void ParseList_MixedTypes_Success()
    {
        // Arrange & Act
        var list = StructuredFieldParser.ParseList("42, \"hello\", foo");

        // Assert
        list.Count.ShouldBe(3);
        list[0].Item.Value.ShouldBeOfType<IntegerItem>();
        list[1].Item.Value.ShouldBeOfType<StringItem>();
        list[2].Item.Value.ShouldBeOfType<TokenItem>();
    }

    [Fact]
    public void ParseList_InnerList_Success()
    {
        // Arrange & Act
        var list = StructuredFieldParser.ParseList("(1 2 3)");

        // Assert
        list.Count.ShouldBe(1);
        list[0].IsInnerList.ShouldBeTrue();
        
        var innerList = list[0].InnerList;
        innerList.Count.ShouldBe(3);
        ((IntegerItem)innerList[0].Value).LongValue.ShouldBe(1);
        ((IntegerItem)innerList[1].Value).LongValue.ShouldBe(2);
        ((IntegerItem)innerList[2].Value).LongValue.ShouldBe(3);
    }

    [Fact]
    public void ParseList_MultipleInnerLists_Success()
    {
        // Arrange & Act
        var list = StructuredFieldParser.ParseList("(1 2), (3 4)");

        // Assert
        list.Count.ShouldBe(2);
        list[0].IsInnerList.ShouldBeTrue();
        list[1].IsInnerList.ShouldBeTrue();
        
        list[0].InnerList.Count.ShouldBe(2);
        list[1].InnerList.Count.ShouldBe(2);
    }

    [Fact]
    public void ParseList_ItemsWithParameters_Success()
    {
        // Arrange & Act
        var list = StructuredFieldParser.ParseList("foo;a=1, bar;b=2");

        // Assert
        list.Count.ShouldBe(2);
        list[0].Item.Parameters["a"].ShouldBeOfType<IntegerItem>();
        list[1].Item.Parameters["b"].ShouldBeOfType<IntegerItem>();
    }

    [Fact]
    public void ParseList_InnerListWithParameters_Success()
    {
        // Arrange & Act
        var list = StructuredFieldParser.ParseList("(1 2);a=foo");

        // Assert
        list.Count.ShouldBe(1);
        list[0].IsInnerList.ShouldBeTrue();
        
        var innerList = list[0].InnerList;
        innerList.Parameters.Count.ShouldBe(1);
        innerList.Parameters["a"].ShouldBeOfType<Http.StructuredFieldValues.TokenItem>();
    }

    [Fact]
    public void ParseList_WithWhitespace_Success()
    {
        // Arrange & Act
        var list = StructuredFieldParser.ParseList("  foo  ,  bar  ");

        // Assert
        list.Count.ShouldBe(2);
        ((TokenItem)list[0].Item.Value).TokenValue.ShouldBe("foo");
        ((TokenItem)list[1].Item.Value).TokenValue.ShouldBe("bar");
    }

    [Fact]
    public void ParseList_EmptyInnerList_Success()
    {
        // Arrange & Act
        var list = StructuredFieldParser.ParseList("()");

        // Assert
        list.Count.ShouldBe(1);
        list[0].IsInnerList.ShouldBeTrue();
        list[0].InnerList.Count.ShouldBe(0);
    }

    [Fact]
    public void ParseList_TrailingComma_ThrowsException() =>
        // Arrange & Act & Assert
        Should.Throw<StructuredFieldParseException>(() => 
            StructuredFieldParser.ParseList("foo,"));
}
