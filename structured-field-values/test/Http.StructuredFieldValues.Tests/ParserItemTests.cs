// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.StructuredFieldValues;

public class ParserItemTests
{
    [Fact]
    public void ParseItem_Integer_Success()
    {
        // Arrange & Act
        var item = StructuredFieldParser.ParseItem("42");

        // Assert
        item.Value.ShouldBeOfType<IntegerItem>();
        var intItem = (IntegerItem)item.Value;
        intItem.LongValue.ShouldBe(42);
    }

    [Fact]
    public void ParseItem_NegativeInteger_Success()
    {
        // Arrange & Act
        var item = StructuredFieldParser.ParseItem("-17");

        // Assert
        item.Value.ShouldBeOfType<IntegerItem>();
        var intItem = (IntegerItem)item.Value;
        intItem.LongValue.ShouldBe(-17);
    }

    [Fact]
    public void ParseItem_Decimal_Success()
    {
        // Arrange & Act
        var item = StructuredFieldParser.ParseItem("3.14");

        // Assert
        item.Value.ShouldBeOfType<DecimalItem>();
        var decItem = (DecimalItem)item.Value;
        decItem.DecimalValue.ShouldBe(3.14m);
    }

    [Fact]
    public void ParseItem_String_Success()
    {
        // Arrange & Act
        var item = StructuredFieldParser.ParseItem("\"hello world\"");

        // Assert
        item.Value.ShouldBeOfType<StringItem>();
        var strItem = (StringItem)item.Value;
        strItem.StringValue.ShouldBe("hello world");
    }

    [Fact]
    public void ParseItem_StringWithEscapes_Success()
    {
        // Arrange & Act
        var item = StructuredFieldParser.ParseItem("\"hello \\\"world\\\"\"");

        // Assert
        item.Value.ShouldBeOfType<StringItem>();
        var strItem = (StringItem)item.Value;
        strItem.StringValue.ShouldBe("hello \"world\"");
    }

    [Fact]
    public void ParseItem_Token_Success()
    {
        // Arrange & Act
        var item = StructuredFieldParser.ParseItem("application/json");

        // Assert
        item.Value.ShouldBeOfType<TokenItem>();
        var tokItem = (TokenItem)item.Value;
        tokItem.TokenValue.ShouldBe("application/json");
    }

    [Fact]
    public void ParseItem_ByteSequence_Success()
    {
        // Arrange & Act
        var item = StructuredFieldParser.ParseItem(":aGVsbG8=:");

        // Assert
        item.Value.ShouldBeOfType<ByteSequenceItem>();
        var byteItem = (ByteSequenceItem)item.Value;
        byteItem.Base64Value.ShouldBe("aGVsbG8=");
    }

    [Fact]
    public void ParseItem_BooleanTrue_Success()
    {
        // Arrange & Act
        var item = StructuredFieldParser.ParseItem("?1");

        // Assert
        item.Value.ShouldBeOfType<BooleanItem>();
        var boolItem = (BooleanItem)item.Value;
        boolItem.BooleanValue.ShouldBeTrue();
    }

    [Fact]
    public void ParseItem_BooleanFalse_Success()
    {
        // Arrange & Act
        var item = StructuredFieldParser.ParseItem("?0");

        // Assert
        item.Value.ShouldBeOfType<BooleanItem>();
        var boolItem = (BooleanItem)item.Value;
        boolItem.BooleanValue.ShouldBeFalse();
    }

    [Fact]
    public void ParseItem_WithParameters_Success()
    {
        // Arrange & Act
        var item = StructuredFieldParser.ParseItem("text/html;q=0.9");

        // Assert
        item.Value.ShouldBeOfType<TokenItem>();
        item.Parameters.Count.ShouldBe(1);
        item.Parameters.ContainsKey("q").ShouldBeTrue();
        
        var qValue = item.Parameters["q"];
        qValue.ShouldBeOfType<DecimalItem>();
        ((DecimalItem)qValue!).DecimalValue.ShouldBe(0.9m);
    }

    [Fact]
    public void ParseItem_WithMultipleParameters_Success()
    {
        // Arrange & Act
        var item = StructuredFieldParser.ParseItem("foo;a=1;b=2");

        // Assert
        item.Value.ShouldBeOfType<TokenItem>();
        item.Parameters.Count.ShouldBe(2);
        item.Parameters["a"].ShouldBeOfType<IntegerItem>();
        item.Parameters["b"].ShouldBeOfType<IntegerItem>();
    }

    [Fact]
    public void ParseItem_WithParameterNoValue_Success()
    {
        // Arrange & Act
        var item = StructuredFieldParser.ParseItem("foo;bar");

        // Assert
        item.Value.ShouldBeOfType<TokenItem>();
        item.Parameters.Count.ShouldBe(1);
        item.Parameters.ContainsKey("bar").ShouldBeTrue();
        item.Parameters["bar"].ShouldBeSameAs(BooleanItem.True);
    }

    [Fact]
    public void ParseItem_WithWhitespace_Success()
    {
        // Arrange & Act
        var item = StructuredFieldParser.ParseItem("  42  ");

        // Assert
        item.Value.ShouldBeOfType<IntegerItem>();
        ((IntegerItem)item.Value).LongValue.ShouldBe(42);
    }

    [Fact]
    public void ParseItem_InvalidInput_ThrowsException() =>
        // Arrange & Act & Assert
        Should.Throw<StructuredFieldParseException>(() => 
            StructuredFieldParser.ParseItem("@invalid"));

    [Fact]
    public void ParseItem_UnterminatedString_ThrowsException() =>
        // Arrange & Act & Assert
        Should.Throw<StructuredFieldParseException>(() => 
            StructuredFieldParser.ParseItem("\"unterminated"));
}
