// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using Shouldly;

namespace DamianH.Http.StructuredFieldValues;

public class ProtocolRegressionTests
{
    [Theory]
    [InlineData("999999999999999", ItemType.Integer)]
    [InlineData("-999999999999999", ItemType.Integer)]
    [InlineData("999999999999.999", ItemType.Decimal)]
    [InlineData("-999999999999.999", ItemType.Decimal)]
    [InlineData("@999999999999999", ItemType.Date)]
    [InlineData("@-999999999999999", ItemType.Date)]
    [InlineData("%\"%f0%9f%9a%80\"", ItemType.DisplayString)]
    public void BoundaryValues_RoundTrip(string wire, ItemType kind)
    {
        var item = StructuredFieldParser.ParseItem(wire);
        item.Type.ShouldBe(kind);
        StructuredFieldSerializer.SerializeItem(item).ShouldBe(wire);
        StructuredFieldParser.ParseBareItem(wire).ShouldBe(item.Value);
    }

    [Theory]
    [InlineData("000000000000000", "0")]
    [InlineData("-000000000000000", "0")]
    [InlineData("000000000000.000", "0.0")]
    [InlineData("-0.000", "0.0")]
    [InlineData("1.000", "1.0")]
    [InlineData("1.230", "1.23")]
    [InlineData("@-000", "@0")]
    [InlineData(":YQ:", ":YQ==:")]
    [InlineData(":YQ=:", ":YQ==:")]
    [InlineData(":aGVsbG8:", ":aGVsbG8=:")]
    [InlineData(":iZ==:", ":iQ==:")]
    [InlineData("%\"%61%22%25\\\"", "%\"a%22%25\\\"")]
    [InlineData("x;a=?1;b=?0", "x;a;b=?0")]
    public void NonCanonicalInput_IsCanonicalized(string input, string canonical) =>
        StructuredFieldSerializer.SerializeItem(StructuredFieldParser.ParseItem(input)).ShouldBe(canonical);

    [Theory]
    [InlineData("")]
    [InlineData("0000000000000000")]
    [InlineData("-0000000000000000")]
    [InlineData("0000000000000.0")]
    [InlineData("1000000000000.0")]
    [InlineData("9999999999999999")]
    [InlineData("0.0000")]
    [InlineData("1.")]
    [InlineData("-")]
    [InlineData("+1")]
    [InlineData("@")]
    [InlineData("@1.0")]
    [InlineData("@+1")]
    [InlineData("@1000000000000000")]
    [InlineData("%\"%C3%BC\"")]
    [InlineData("%\"%c0%af\"")]
    [InlineData("%\"%ed%a0%80\"")]
    [InlineData("%\"%f4%90%80%80\"")]
    [InlineData("%\"%80\"")]
    [InlineData("%\"%e2%82\"")]
    [InlineData("%\"%0\"")]
    [InlineData("%\"%xz\"")]
    [InlineData("%\"é\"")]
    [InlineData("%\"\ud800\"")]
    [InlineData("%\"\n\"")]
    [InlineData("%\"missing")]
    [InlineData(":A:")]
    [InlineData(":====:")]
    [InlineData(":Y=Q=:")]
    [InlineData(":Y Q=:")]
    [InlineData(":YQ==")]
    [InlineData("\"\\x\"")]
    [InlineData("\"é\"")]
    [InlineData("?2")]
    [InlineData("x;_a")]
    [InlineData("x;-a")]
    [InlineData("x;.a")]
    [InlineData("x;0a")]
    [InlineData("x;a=")]
    [InlineData("x; a =1")]
    [InlineData("x ;a")]
    [InlineData("x;\ta")]
    [InlineData("\tx")]
    [InlineData("x\t")]
    [InlineData("x\n")]
    public void InvalidItems_AlwaysProducePositionedParseErrors(string input)
    {
        var error = Should.Throw<StructuredFieldParseException>(() => StructuredFieldParser.ParseItem(input));
        error.Position.ShouldNotBeNull();
        error.Position.Value.ShouldBeInRange(0, input.Length);
    }

    [Theory]
    [InlineData("(1\t2)")]
    [InlineData("(\t1)")]
    [InlineData("(1\t)")]
    [InlineData("(1")]
    [InlineData("(1, 2)")]
    [InlineData("((1))")]
    [InlineData("\tx")]
    [InlineData("\t")]
    [InlineData("x, \t")]
    [InlineData("x;\ta, y")]
    public void InvalidLists_AlwaysProducePositionedParseErrors(string input) =>
        Should.Throw<StructuredFieldParseException>(() => StructuredFieldParser.ParseList(input)).Position.ShouldNotBeNull();

    [Theory]
    [InlineData("_a=1")]
    [InlineData("-a=1")]
    [InlineData(".a=1")]
    [InlineData("0a=1")]
    [InlineData("A=1")]
    [InlineData("a =1")]
    [InlineData("a= 1")]
    [InlineData("a=1;\tp")]
    [InlineData("\ta=1")]
    [InlineData("a=1,")]
    public void InvalidDictionaries_AlwaysProducePositionedParseErrors(string input) =>
        Should.Throw<StructuredFieldParseException>(() => StructuredFieldParser.ParseDictionary(input)).Position.ShouldNotBeNull();

    [Fact]
    public void SpacesAndOws_AreAcceptedOnlyAtTheirGrammarLocations()
    {
        StructuredFieldParser.ParseItem("  x;  a  ").Parameters["a"].ShouldBe(BooleanItem.True);
        var list = StructuredFieldParser.ParseList("  ( 1  2 );  a \t, \tx\t");
        list.Count.ShouldBe(2);
        list[0].InnerList.Count.ShouldBe(2);
        list[0].Parameters["a"].ShouldBe(BooleanItem.True);
        StructuredFieldParser.ParseDictionary(" a=1\t,\tb=2\t").Count.ShouldBe(2);
        StructuredFieldParser.ParseList("   ").Count.ShouldBe(0);
        StructuredFieldParser.ParseDictionary("   ").Count.ShouldBe(0);
    }

    [Fact]
    public void DuplicateKeysAndParameters_LastWinsInOriginalPosition()
    {
        var dictionary = StructuredFieldParser.ParseDictionary("a=1;x=1;y=2;x=?1, b=2, a=3;z=4;z=5;flag;flag=?0");
        dictionary.Select(x => x.Key).ShouldBe(["a", "b"]);
        StructuredFieldSerializer.SerializeDictionary(dictionary).ShouldBe("a=3;z=5;flag=?0, b=2");

        var item = StructuredFieldParser.ParseItem("x;a=1;b=2;a=?1");
        item.Parameters.Select(x => x.Key).ShouldBe(["a", "b"]);
        item.Parameters["a"].ShouldBeSameAs(BooleanItem.True);
        StructuredFieldSerializer.SerializeItem(item).ShouldBe("x;a;b=2");

        var list = StructuredFieldParser.ParseList("(1;a=1;a=2);a=3;b=4;a=5");
        StructuredFieldSerializer.SerializeList(list).ShouldBe("(1;a=2);a=5;b=4");
    }

    [Fact]
    public void BooleanSingletons_DoNotShareMutableParameters()
    {
        var first = StructuredFieldParser.ParseItem("?1;a");
        var second = StructuredFieldParser.ParseItem("?1;b");
        first.Value.ShouldBeSameAs(second.Value);
        first.Parameters.Clear();
        second.Parameters.ContainsKey("b").ShouldBeTrue();
        StructuredFieldParser.ParseItem("?1").Parameters.Count.ShouldBe(0);
    }

    [Fact]
    public void NewTypes_AreSupportedInEveryBareValuePosition()
    {
        const string item = "@999999999999999;label=%\"caf%c3%a9\";when=@-999999999999999";
        StructuredFieldSerializer.SerializeItem(StructuredFieldParser.ParseItem(item)).ShouldBe(item);
        const string list = "%\"a\", (@1;p=%\"b\" %\"c\");at=@2";
        StructuredFieldSerializer.SerializeList(StructuredFieldParser.ParseList(list)).ShouldBe(list);
        const string dictionary = "a=@1, b=%\"b\", c=(@2 %\"c\");p=@3";
        StructuredFieldSerializer.SerializeDictionary(StructuredFieldParser.ParseDictionary(dictionary)).ShouldBe(dictionary);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("de-DE")]
    [InlineData("ar-SA")]
    public void NumericConstructionAndWriting_AreCultureIndependent(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var number = new DecimalItem(999_999_999_999.999m);
            StructuredFieldSerializer.SerializeBareItem(number).ShouldBe("999999999999.999");
            StructuredFieldParser.ParseBareItem("999999999999.999").ShouldBe(number);
            StructuredFieldSerializer.SerializeBareItem(new DecimalItem(1.23000m)).ShouldBe("1.23");
            StructuredFieldSerializer.SerializeBareItem(new IntegerItem(-999_999_999_999_999)).ShouldBe("-999999999999999");
            StructuredFieldSerializer.SerializeBareItem(new DateItem(-999_999_999_999_999)).ShouldBe("@-999999999999999");
            Should.Throw<ArgumentException>(() => new DecimalItem(1.0001m));
            Should.Throw<ArgumentOutOfRangeException>(() => new DecimalItem(decimal.MinValue));
            Should.Throw<ArgumentOutOfRangeException>(() => new DecimalItem(decimal.MaxValue));
            Should.Throw<ArgumentOutOfRangeException>(() => new DecimalItem(1_000_000_000_000m));
            Should.Throw<ArgumentOutOfRangeException>(() => new IntegerItem(long.MinValue));
            Should.Throw<ArgumentOutOfRangeException>(() => new IntegerItem(long.MaxValue));
            Should.Throw<ArgumentOutOfRangeException>(() => new DateItem(long.MinValue));
            Should.Throw<ArgumentOutOfRangeException>(() => new DateItem(long.MaxValue));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void BareParsing_RejectsParametersAndPreservesNullMisuse()
    {
        Should.Throw<StructuredFieldParseException>(() => StructuredFieldParser.ParseBareItem("1;a"));
        Should.Throw<ArgumentNullException>(() => StructuredFieldParser.ParseBareItem(null!));
        Should.Throw<ArgumentNullException>(() => StructuredFieldParser.ParseItem(null!));
        Should.Throw<ArgumentNullException>(() => StructuredFieldParser.ParseList(null!));
        Should.Throw<ArgumentNullException>(() => StructuredFieldParser.ParseDictionary(null!));
    }
}
