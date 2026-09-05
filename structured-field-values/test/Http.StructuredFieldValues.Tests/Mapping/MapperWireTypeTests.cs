// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.StructuredFieldValues.Mapping;

public class MapperWireTypeTests
{
    [Theory]
    [InlineData(-999999999999999L)]
    [InlineData(-1L)]
    [InlineData(0L)]
    [InlineData(999999999999999L)]
    public void DateOverride_RoundTripsFullRfcRangeInEveryPosition(long seconds)
    {
        var item = StructuredFieldMapper<DateModel>.Item(b => b
            .Value(x => x.Seconds, type: ItemType.Date)
            .Parameter("at", x => x.OptionalSeconds, type: ItemType.Date));
        var dictionary = StructuredFieldMapper<DateModel>.Dictionary(b => b
            .Member("at", x => x.Seconds, type: ItemType.Date)
            .InnerList("dates", x => x.Dates, type: ItemType.Date));
        var list = StructuredFieldMapper<DateModel>.List(b => b.Elements(x => x.Dates, type: ItemType.Date));

        var value = new DateModel { Seconds = seconds, OptionalSeconds = seconds, Dates = [seconds] };
        var date = $"@{seconds}";
        item.Serialize(value).ShouldBe($"{date};at={date}");
        item.Parse($"{date};at={date}").Seconds.ShouldBe(seconds);
        item.Parse($"{date};at={date}").OptionalSeconds.ShouldBe(seconds);
        dictionary.Serialize(value).ShouldBe($"at={date}, dates=({date})");
        var parsed = dictionary.Parse($"at={date}, dates=({date})");
        parsed.Seconds.ShouldBe(seconds);
        parsed.Dates.ShouldBe([seconds]);
        list.Serialize(value).ShouldBe(date);
        list.Parse(date).Dates.ShouldBe([seconds]);
    }

    [Fact]
    public void DisplayStringOverride_RoundTripsInEveryPosition()
    {
        const string text = "café 😊";
        const string wire = "%\"caf%c3%a9 %f0%9f%98%8a\"";
        var item = StructuredFieldMapper<TextModel>.Item(b => b
            .Value(x => x.Text, type: ItemType.DisplayString)
            .Parameter("label", x => x.Parameter, type: ItemType.DisplayString));
        var dictionary = StructuredFieldMapper<TextModel>.Dictionary(b => b
            .Member("label", x => x.Text, type: ItemType.DisplayString)
            .InnerList("labels", x => x.Texts, type: ItemType.DisplayString));
        var list = StructuredFieldMapper<TextModel>.List(b => b.Elements(x => x.Texts, type: ItemType.DisplayString));

        var value = new TextModel { Text = text, Parameter = text, Texts = [text] };
        item.Serialize(value).ShouldBe($"{wire};label={wire}");
        var parsedItem = item.Parse($"{wire};label={wire}");
        parsedItem.Text.ShouldBe(text);
        parsedItem.Parameter.ShouldBe(text);
        dictionary.Serialize(value).ShouldBe($"label={wire}, labels=({wire})");
        var parsedDictionary = dictionary.Parse($"label={wire}, labels=({wire})");
        parsedDictionary.Text.ShouldBe(text);
        parsedDictionary.Texts.ShouldBe([text]);
        list.Serialize(value).ShouldBe(wire);
        list.Parse(wire).Texts.ShouldBe([text]);
    }

    [Theory]
    [InlineData(ItemType.String, "\"text\"")]
    [InlineData(ItemType.Token, "text")]
    [InlineData(ItemType.DisplayString, "%\"text\"")]
    public void StringWireOverrides_AreUnifiedAcrossEveryMapping(ItemType type, string wire)
    {
        var item = StructuredFieldMapper<TextModel>.Item(b => b
            .Value(x => x.Text, type: type)
            .Parameter("p", x => x.Parameter, type: type));
        var dictionary = StructuredFieldMapper<TextModel>.Dictionary(b => b
            .Member("m", x => x.Text, type: type)
            .InnerList("values", x => x.Texts, type: type));
        var list = StructuredFieldMapper<TextModel>.List(b => b.Elements(x => x.Texts, type: type));
        var value = new TextModel { Text = "text", Parameter = "text", Texts = ["text"] };

        item.Serialize(item.Parse($"{wire};p={wire}")).ShouldBe($"{wire};p={wire}");
        dictionary.Serialize(dictionary.Parse($"m={wire}, values=({wire})")).ShouldBe($"m={wire}, values=({wire})");
        list.Serialize(list.Parse(wire)).ShouldBe(wire);
        item.Serialize(value).ShouldBe($"{wire};p={wire}");
        dictionary.Serialize(value).ShouldBe($"m={wire}, values=({wire})");
        list.Serialize(value).ShouldBe(wire);
    }

    [Theory]
    [InlineData(ItemType.Integer)]
    [InlineData(ItemType.Decimal)]
    [InlineData(ItemType.Boolean)]
    [InlineData(ItemType.ByteSequence)]
    [InlineData(ItemType.Date)]
    [InlineData((ItemType)100)]
    public void IncompatibleStringWireTypes_AreRejectedAtRegistration(ItemType type)
    {
        Should.Throw<ArgumentException>(() => new ItemBuilder<TextModel>().Value(x => x.Text, type: type));
        Should.Throw<ArgumentException>(() => new ItemBuilder<TextModel>().Parameter("p", x => x.Parameter, type: type));
        Should.Throw<ArgumentException>(() => new DictionaryBuilder<TextModel>().Member("m", x => x.Text, type: type));
        Should.Throw<ArgumentException>(() => new DictionaryBuilder<TextModel>().InnerList("values", x => x.Texts, type: type));
        Should.Throw<ArgumentException>(() => new ListBuilder<TextModel>().Elements(x => x.Texts, type: type));
    }

    [Theory]
    [InlineData(ItemType.String)]
    [InlineData(ItemType.Token)]
    [InlineData(ItemType.DisplayString)]
    [InlineData(ItemType.Decimal)]
    [InlineData(ItemType.Boolean)]
    [InlineData(ItemType.ByteSequence)]
    public void IncompatibleIntegerWireTypes_AreRejectedAtRegistration(ItemType type)
    {
        Should.Throw<ArgumentException>(() => new ItemBuilder<DateModel>().Value(x => x.Seconds, type: type));
        Should.Throw<ArgumentException>(() => new DictionaryBuilder<DateModel>().Member("at", x => x.Seconds, type: type));
        Should.Throw<ArgumentException>(() => new DictionaryBuilder<DateModel>().InnerList("dates", x => x.Dates, type: type));
        Should.Throw<ArgumentException>(() => new ListBuilder<DateModel>().Elements(x => x.Dates, type: type));
    }

    [Fact]
    public void DefaultInferenceAndExplicitMatchingTypes_Agree()
    {
        AssertScalar(42, ItemType.Integer, "42");
        AssertScalar(42L, ItemType.Integer, "42");
        AssertScalar(12.345m, ItemType.Decimal, "12.345");
        AssertScalar(true, ItemType.Boolean, "?1");
        AssertScalar("text", ItemType.String, "\"text\"");
        AssertScalar(new byte[] { 1, 2, 3 }, ItemType.ByteSequence, ":AQID:");
    }

    [Fact]
    public void InvalidBooleanDecimalAndByteOverrides_AreRejected()
    {
        Should.Throw<ArgumentException>(() =>
            new ItemBuilder<Scalar<bool>>().Value(x => x.Value, type: ItemType.Integer));
        Should.Throw<ArgumentException>(() =>
            new ItemBuilder<Scalar<decimal>>().Value(x => x.Value, type: ItemType.Date));
        Should.Throw<ArgumentException>(() =>
            new ItemBuilder<Scalar<byte[]>>().Value(x => x.Value, type: ItemType.String));
        Should.Throw<NotSupportedException>(() =>
            new ItemBuilder<Scalar<DateTimeOffset>>().Value(x => x.Value, type: ItemType.Date));
    }

    [Fact]
    public void IntegerDateOverride_HandlesNarrowingAndOverflow()
    {
        var mapper = StructuredFieldMapper<Scalar<int?>>.Item(b => b.Value(x => x.Value, type: ItemType.Date));
        mapper.Parse("@2147483647").Value.ShouldBe(int.MaxValue);
        mapper.Serialize(new Scalar<int?> { Value = int.MinValue }).ShouldBe("@-2147483648");
        mapper.TryParse("@2147483648", out _).ShouldBeFalse();
        Should.Throw<StructuredFieldParseException>(() => mapper.Parse("@-2147483649"));
    }

    [Fact]
    public void Overrides_RequireExactWireTypesOnRead()
    {
        var date = StructuredFieldMapper<DateModel>.Item(b => b.Value(x => x.Seconds, type: ItemType.Date));
        var display = StructuredFieldMapper<TextModel>.Item(b => b.Value(x => x.Text, type: ItemType.DisplayString));
        date.TryParse("123", out _).ShouldBeFalse();
        display.TryParse("\"plain\"", out _).ShouldBeFalse();
    }

    [Fact]
    public void ByteSequenceValues_AreIndependentCopies()
    {
        var mapper = StructuredFieldMapper<Scalar<byte[]>>.Item(b => b.Value(x => x.Value));
        var first = mapper.Parse(":AQID:");
        var second = mapper.Parse(":AQID:");
        first.Value[0] = 255;
        second.Value.ShouldBe(new byte[] { 1, 2, 3 });
        mapper.Serialize(second).ShouldBe(":AQID:");
    }

    private static void AssertScalar<TValue>(TValue value, ItemType type, string wire)
    {
        var inferred = StructuredFieldMapper<Scalar<TValue>>.Item(b => b.Value(x => x.Value));
        var explicitType = StructuredFieldMapper<Scalar<TValue>>.Item(b => b.Value(x => x.Value, type: type));
        inferred.Serialize(new Scalar<TValue> { Value = value }).ShouldBe(wire);
        explicitType.Serialize(new Scalar<TValue> { Value = value }).ShouldBe(wire);
        inferred.Serialize(inferred.Parse(wire)).ShouldBe(wire);
        explicitType.Serialize(explicitType.Parse(wire)).ShouldBe(wire);
    }

    public class Scalar<TValue>
    {
        public TValue Value { get; set; } = default!;
    }

    public class DateModel
    {
        public long Seconds { get; set; }
        public long? OptionalSeconds { get; set; }
        public IReadOnlyList<long> Dates { get; set; } = [];
    }

    public class TextModel
    {
        public string Text { get; set; } = "";
        public string? Parameter { get; set; }
        public IReadOnlyList<string> Texts { get; set; } = [];
    }
}
