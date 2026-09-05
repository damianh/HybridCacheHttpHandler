// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.StructuredFieldValues;

public class SerializerDictionaryTests
{
    [Fact]
    public void SerializeDictionary_Empty_Success()
    {
        // Arrange
        var dict = new StructuredFieldDictionary();

        // Act
        var output = StructuredFieldSerializer.SerializeDictionary(dict);

        // Assert
        output.ShouldBe("");
    }

    [Fact]
    public void SerializeDictionary_SingleMember_Success()
    {
        // Arrange
        var dict = new StructuredFieldDictionary { { "a", new IntegerItem(1) } };

        // Act
        var output = StructuredFieldSerializer.SerializeDictionary(dict);

        // Assert
        output.ShouldBe("a=1");
    }

    [Fact]
    public void SerializeDictionary_MultipleMembers_Success()
    {
        // Arrange
        var dict = new StructuredFieldDictionary
        {
            { "a", new IntegerItem(1) },
            { "b", new IntegerItem(2) },
            { "c", new IntegerItem(3) }
        };

        // Act
        var output = StructuredFieldSerializer.SerializeDictionary(dict);

        // Assert
        output.ShouldBe("a=1, b=2, c=3");
    }

    [Fact]
    public void SerializeDictionary_MixedTypes_Success()
    {
        // Arrange
        var dict = new StructuredFieldDictionary
        {
            { "a", new IntegerItem(42) },
            { "b", new StringItem("hello") },
            { "c", new Http.StructuredFieldValues.TokenItem("foo") }
        };

        // Act
        var output = StructuredFieldSerializer.SerializeDictionary(dict);

        // Assert
        output.ShouldBe("a=42, b=\"hello\", c=foo");
    }

    [Fact]
    public void SerializeDictionary_BooleanTrue_Success()
    {
        // Arrange
        var dict = new StructuredFieldDictionary { { "foo", BooleanItem.True } };

        // Act
        var output = StructuredFieldSerializer.SerializeDictionary(dict);

        // Assert
        output.ShouldBe("foo");
    }

    [Fact]
    public void SerializeDictionary_BooleanFalse_Success()
    {
        // Arrange
        var dict = new StructuredFieldDictionary { { "foo", BooleanItem.False } };

        // Act
        var output = StructuredFieldSerializer.SerializeDictionary(dict);

        // Assert
        output.ShouldBe("foo=?0");
    }

    [Fact]
    public void SerializeDictionary_BooleanTrueWithParameters_Success()
    {
        // Arrange
        var dict = new StructuredFieldDictionary();
        var item = new StructuredFieldItem(BooleanItem.True);
        item.Parameters.Add("bar", new Http.StructuredFieldValues.TokenItem("baz"));
        dict.Add("foo", item);

        // Act
        var output = StructuredFieldSerializer.SerializeDictionary(dict);

        // Assert
        output.ShouldBe("foo;bar=baz");
    }

    [Fact]
    public void SerializeDictionary_InnerList_Success()
    {
        // Arrange
        var dict = new StructuredFieldDictionary
        { { "a", new InnerList(new StructuredFieldItem[]
        {
            new IntegerItem(1),
            new IntegerItem(2),
            new IntegerItem(3)
        }) } };

        // Act
        var output = StructuredFieldSerializer.SerializeDictionary(dict);

        // Assert
        output.ShouldBe("a=(1 2 3)");
    }

    [Fact]
    public void SerializeDictionary_Priority_Success()
    {
        // Arrange
        var dict = new StructuredFieldDictionary
        {
            { "u", new IntegerItem(3) },
            { "i", new BooleanItem(true) }
        };

        // Act
        var output = StructuredFieldSerializer.SerializeDictionary(dict);

        // Assert
        output.ShouldBe("u=3, i");
    }

    [Fact]
    public void SerializeDictionary_RoundTrip_Success()
    {
        // Arrange
        var original = "a=1, b=2, c=3";

        // Act
        var parsed = StructuredFieldParser.ParseDictionary(original);
        var serialized = StructuredFieldSerializer.SerializeDictionary(parsed);

        // Assert
        serialized.ShouldBe(original);
    }

    [Fact]
    public void SerializeDictionary_PriorityRoundTrip_Success()
    {
        // Arrange
        var original = "u=3, i";

        // Act
        var parsed = StructuredFieldParser.ParseDictionary(original);
        var serialized = StructuredFieldSerializer.SerializeDictionary(parsed);

        // Assert
        serialized.ShouldBe(original);
    }

    [Fact]
    public void SerializeDictionary_ComplexRoundTrip_Success()
    {
        // Arrange
        var original = "a=1, b=\"hello\", c=(1 2 3), d";

        // Act
        var parsed = StructuredFieldParser.ParseDictionary(original);
        var serialized = StructuredFieldSerializer.SerializeDictionary(parsed);

        // Assert
        serialized.ShouldBe(original);
    }

    [Fact]
    public void SerializeDictionary_MaintainsInsertionOrder_Success()
    {
        // Arrange
        var dict = new StructuredFieldDictionary
        {
            { "z", new IntegerItem(1) },
            { "a", new IntegerItem(2) },
            { "m", new IntegerItem(3) }
        };

        // Act
        var output = StructuredFieldSerializer.SerializeDictionary(dict);

        // Assert
        output.ShouldBe("z=1, a=2, m=3");
    }
}
