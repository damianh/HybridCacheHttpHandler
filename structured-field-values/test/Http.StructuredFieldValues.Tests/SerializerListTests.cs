// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.StructuredFieldValues;

public class SerializerListTests
{
    [Fact]
    public void SerializeList_Empty_Success()
    {
        // Arrange
        var list = new StructuredFieldList();

        // Act
        var output = StructuredFieldSerializer.SerializeList(list);

        // Assert
        output.ShouldBe("");
    }

    [Fact]
    public void SerializeList_SingleItem_Success()
    {
        // Arrange
        var list = new StructuredFieldList();
        list.Add(new Http.StructuredFieldValues.TokenItem("foo"));

        // Act
        var output = StructuredFieldSerializer.SerializeList(list);

        // Assert
        output.ShouldBe("foo");
    }

    [Fact]
    public void SerializeList_MultipleItems_Success()
    {
        // Arrange
        var list = new StructuredFieldList();
        list.Add(new Http.StructuredFieldValues.TokenItem("foo"));
        list.Add(new Http.StructuredFieldValues.TokenItem("bar"));
        list.Add(new Http.StructuredFieldValues.TokenItem("baz"));

        // Act
        var output = StructuredFieldSerializer.SerializeList(list);

        // Assert
        output.ShouldBe("foo, bar, baz");
    }

    [Fact]
    public void SerializeList_MixedTypes_Success()
    {
        // Arrange
        var list = new StructuredFieldList();
        list.Add(new IntegerItem(42));
        list.Add(new StringItem("hello"));
        list.Add(new Http.StructuredFieldValues.TokenItem("foo"));

        // Act
        var output = StructuredFieldSerializer.SerializeList(list);

        // Assert
        output.ShouldBe("42, \"hello\", foo");
    }

    [Fact]
    public void SerializeList_InnerList_Success()
    {
        // Arrange
        var list = new StructuredFieldList();
        var innerList = new InnerList(new StructuredFieldItem[]
        {
            new IntegerItem(1),
            new IntegerItem(2),
            new IntegerItem(3)
        });
        list.Add(innerList);

        // Act
        var output = StructuredFieldSerializer.SerializeList(list);

        // Assert
        output.ShouldBe("(1 2 3)");
    }

    [Fact]
    public void SerializeList_MultipleInnerLists_Success()
    {
        // Arrange
        var list = new StructuredFieldList();
        list.Add(new InnerList([new IntegerItem(1), new IntegerItem(2)]));
        list.Add(new InnerList([new IntegerItem(3), new IntegerItem(4)]));

        // Act
        var output = StructuredFieldSerializer.SerializeList(list);

        // Assert
        output.ShouldBe("(1 2), (3 4)");
    }

    [Fact]
    public void SerializeList_ItemWithParameters_Success()
    {
        // Arrange
        var list = new StructuredFieldList();
        var item = new StructuredFieldItem(new TokenItem("foo"),
            new Parameters { { "a", new IntegerItem(1) } });
        list.Add(item);

        // Act
        var output = StructuredFieldSerializer.SerializeList(list);

        // Assert
        output.ShouldBe("foo;a=1");
    }

    [Fact]
    public void SerializeList_InnerListWithParameters_Success()
    {
        // Arrange
        var list = new StructuredFieldList();
        var innerList = new InnerList([new IntegerItem(1), new IntegerItem(2)],
            new Parameters { { "a", new TokenItem("foo") } });
        list.Add(innerList);

        // Act
        var output = StructuredFieldSerializer.SerializeList(list);

        // Assert
        output.ShouldBe("(1 2);a=foo");
    }

    [Fact]
    public void SerializeList_EmptyInnerList_Success()
    {
        // Arrange
        var list = new StructuredFieldList();
        list.Add(new InnerList());

        // Act
        var output = StructuredFieldSerializer.SerializeList(list);

        // Assert
        output.ShouldBe("()");
    }

    [Fact]
    public void SerializeList_InnerListItemsWithParameters_Success()
    {
        // Arrange
        var list = new StructuredFieldList();
        var item1 = new StructuredFieldItem(new IntegerItem(1),
            new Parameters { { "a", new TokenItem("x") } });
        var item2 = new StructuredFieldItem(new IntegerItem(2),
            new Parameters { { "b", new TokenItem("y") } });
        list.Add(new InnerList(new[] { item1, item2 }));

        // Act
        var output = StructuredFieldSerializer.SerializeList(list);

        // Assert
        output.ShouldBe("(1;a=x 2;b=y)");
    }

    [Fact]
    public void SerializeList_RoundTrip_Success()
    {
        // Arrange
        var original = "foo, bar, (1 2 3)";

        // Act
        var parsed = StructuredFieldParser.ParseList(original);
        var serialized = StructuredFieldSerializer.SerializeList(parsed);

        // Assert
        serialized.ShouldBe(original);
    }

    [Fact]
    public void SerializeList_ComplexRoundTrip_Success()
    {
        // Arrange
        var original = "foo;a=1, (1 2);b=bar, \"hello\"";

        // Act
        var parsed = StructuredFieldParser.ParseList(original);
        var serialized = StructuredFieldSerializer.SerializeList(parsed);

        // Assert
        serialized.ShouldBe(original);
    }
}
