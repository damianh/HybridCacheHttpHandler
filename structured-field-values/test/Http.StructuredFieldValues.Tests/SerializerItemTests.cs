// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.StructuredFieldValues;

public class SerializerItemTests
{
    [Fact]
    public void SerializeItem_Integer_Success()
    {
        // Arrange
        var item = new IntegerItem(42);

        // Act
        var output = StructuredFieldSerializer.SerializeItem(item);

        // Assert
        output.ShouldBe("42");
    }

    [Fact]
    public void SerializeItem_NegativeInteger_Success()
    {
        // Arrange
        var item = new IntegerItem(-17);

        // Act
        var output = StructuredFieldSerializer.SerializeItem(item);

        // Assert
        output.ShouldBe("-17");
    }

    [Fact]
    public void SerializeItem_Decimal_Success()
    {
        // Arrange
        var item = new DecimalItem(3.14m);

        // Act
        var output = StructuredFieldSerializer.SerializeItem(item);

        // Assert
        output.ShouldBe("3.14");
    }

    [Fact]
    public void SerializeItem_DecimalWithTrailingZeros_Success()
    {
        // Arrange
        var item = new DecimalItem(3.1m);

        // Act
        var output = StructuredFieldSerializer.SerializeItem(item);

        // Assert
        output.ShouldBe("3.1");
    }

    [Fact]
    public void SerializeItem_String_Success()
    {
        // Arrange
        var item = new StringItem("hello world");

        // Act
        var output = StructuredFieldSerializer.SerializeItem(item);

        // Assert
        output.ShouldBe("\"hello world\"");
    }

    [Fact]
    public void SerializeItem_StringWithEscapes_Success()
    {
        // Arrange
        var item = new StringItem("hello \"world\"");

        // Act
        var output = StructuredFieldSerializer.SerializeItem(item);

        // Assert
        output.ShouldBe("\"hello \\\"world\\\"\"");
    }

    [Fact]
    public void SerializeItem_StringWithBackslash_Success()
    {
        // Arrange
        var item = new StringItem("path\\to\\file");

        // Act
        var output = StructuredFieldSerializer.SerializeItem(item);

        // Assert
        output.ShouldBe("\"path\\\\to\\\\file\"");
    }

    [Fact]
    public void SerializeItem_Token_Success()
    {
        // Arrange
        var item = new Http.StructuredFieldValues.TokenItem("application/json");

        // Act
        var output = StructuredFieldSerializer.SerializeItem(item);

        // Assert
        output.ShouldBe("application/json");
    }

    [Fact]
    public void SerializeItem_ByteSequence_Success()
    {
        // Arrange
        var item = ByteSequenceItem.FromBase64("aGVsbG8=");

        // Act
        var output = StructuredFieldSerializer.SerializeItem(item);

        // Assert
        output.ShouldBe(":aGVsbG8=:");
    }

    [Fact]
    public void SerializeItem_BooleanTrue_Success()
    {
        // Arrange
        var item = new BooleanItem(true);

        // Act
        var output = StructuredFieldSerializer.SerializeItem(item);

        // Assert
        output.ShouldBe("?1");
    }

    [Fact]
    public void SerializeItem_BooleanFalse_Success()
    {
        // Arrange
        var item = new BooleanItem(false);

        // Act
        var output = StructuredFieldSerializer.SerializeItem(item);

        // Assert
        output.ShouldBe("?0");
    }

    [Fact]
    public void SerializeItem_WithSingleParameter_Success()
    {
        // Arrange
        var item = new StructuredFieldItem(new TokenItem("text/html"),
            new Parameters { { "q", new DecimalItem(0.9m) } });

        // Act
        var output = StructuredFieldSerializer.SerializeItem(item);

        // Assert
        output.ShouldBe("text/html;q=0.9");
    }

    [Fact]
    public void SerializeItem_WithMultipleParameters_Success()
    {
        // Arrange
        var item = new StructuredFieldItem(new TokenItem("foo"),
            new Parameters
            { 
                { "a", new IntegerItem(1) },
                { "b", new IntegerItem(2) }
            });

        // Act
        var output = StructuredFieldSerializer.SerializeItem(item);

        // Assert
        output.ShouldBe("foo;a=1;b=2");
    }

    [Fact]
    public void SerializeItem_WithParameterNoValue_Success()
    {
        // Arrange
        var item = new StructuredFieldItem(new TokenItem("foo"),
            new Parameters { { "bar", BooleanItem.True } });

        // Act
        var output = StructuredFieldSerializer.SerializeItem(item);

        // Assert
        output.ShouldBe("foo;bar");
    }

    [Fact]
    public void SerializeItem_RoundTrip_Success()
    {
        // Arrange
        var original = "text/html;q=0.9";

        // Act
        var parsed = StructuredFieldParser.ParseItem(original);
        var serialized = StructuredFieldSerializer.SerializeItem(parsed);

        // Assert
        serialized.ShouldBe(original);
    }
}
