// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.StructuredFieldValues;

public class ParserDictionaryTests
{
    [Fact]
    public void ParseDictionary_Empty_Success()
    {
        // Arrange & Act
        var dict = StructuredFieldParser.ParseDictionary("");

        // Assert
        dict.Count.ShouldBe(0);
    }

    [Fact]
    public void ParseDictionary_SingleMember_Success()
    {
        // Arrange & Act
        var dict = StructuredFieldParser.ParseDictionary("a=1");

        // Assert
        dict.Count.ShouldBe(1);
        dict.ContainsKey("a").ShouldBeTrue();
        dict["a"].Item.Value.ShouldBeOfType<IntegerItem>();
        ((IntegerItem)dict["a"].Item.Value).LongValue.ShouldBe(1);
    }

    [Fact]
    public void ParseDictionary_MultipleMembers_Success()
    {
        // Arrange & Act
        var dict = StructuredFieldParser.ParseDictionary("a=1, b=2, c=3");

        // Assert
        dict.Count.ShouldBe(3);
        ((IntegerItem)dict["a"].Item.Value).LongValue.ShouldBe(1);
        ((IntegerItem)dict["b"].Item.Value).LongValue.ShouldBe(2);
        ((IntegerItem)dict["c"].Item.Value).LongValue.ShouldBe(3);
    }

    [Fact]
    public void ParseDictionary_MixedTypes_Success()
    {
        // Arrange & Act
        var dict = StructuredFieldParser.ParseDictionary("a=42, b=\"hello\", c=foo");

        // Assert
        dict.Count.ShouldBe(3);
        dict["a"].Item.Value.ShouldBeOfType<IntegerItem>();
        dict["b"].Item.Value.ShouldBeOfType<StringItem>();
        dict["c"].Item.Value.ShouldBeOfType<TokenItem>();
    }

    [Fact]
    public void ParseDictionary_BooleanTrueNoValue_Success()
    {
        // Arrange & Act
        var dict = StructuredFieldParser.ParseDictionary("foo");

        // Assert
        dict.Count.ShouldBe(1);
        dict["foo"].Item.Value.ShouldBeOfType<BooleanItem>();
        ((BooleanItem)dict["foo"].Item.Value).BooleanValue.ShouldBeTrue();
    }

    [Fact]
    public void ParseDictionary_BooleanWithParameters_Success()
    {
        // Arrange & Act
        var dict = StructuredFieldParser.ParseDictionary("foo;bar=baz");

        // Assert
        dict.Count.ShouldBe(1);
        dict["foo"].Item.Value.ShouldBeOfType<BooleanItem>();
        dict["foo"].Item.Parameters.ContainsKey("bar").ShouldBeTrue();
    }

    [Fact]
    public void ParseDictionary_InnerList_Success()
    {
        // Arrange & Act
        var dict = StructuredFieldParser.ParseDictionary("a=(1 2 3)");

        // Assert
        dict.Count.ShouldBe(1);
        dict["a"].IsInnerList.ShouldBeTrue();
        dict["a"].InnerList.Count.ShouldBe(3);
    }

    [Fact]
    public void ParseDictionary_RealWorldPriority_Success()
    {
        // Arrange & Act
        var dict = StructuredFieldParser.ParseDictionary("u=3, i");

        // Assert
        dict.Count.ShouldBe(2);
        dict["u"].Item.Value.ShouldBeOfType<IntegerItem>();
        ((IntegerItem)dict["u"].Item.Value).LongValue.ShouldBe(3);
        
        dict["i"].Item.Value.ShouldBeOfType<BooleanItem>();
        ((BooleanItem)dict["i"].Item.Value).BooleanValue.ShouldBeTrue();
    }

    [Fact]
    public void ParseDictionary_WithWhitespace_Success()
    {
        // Arrange & Act
        var dict = StructuredFieldParser.ParseDictionary("  a=1  ,  b=2  ");

        // Assert
        dict.Count.ShouldBe(2);
        ((IntegerItem)dict["a"].Item.Value).LongValue.ShouldBe(1);
        ((IntegerItem)dict["b"].Item.Value).LongValue.ShouldBe(2);
    }

    [Fact]
    public void ParseDictionary_TrailingComma_ThrowsException() =>
        // Arrange & Act & Assert
        Should.Throw<StructuredFieldParseException>(() => 
            StructuredFieldParser.ParseDictionary("a=1,"));

    [Fact]
    public void ParseDictionary_InvalidKey_ThrowsException() =>
        // Arrange & Act & Assert
        Should.Throw<StructuredFieldParseException>(() => 
            StructuredFieldParser.ParseDictionary("123=value"));
}
