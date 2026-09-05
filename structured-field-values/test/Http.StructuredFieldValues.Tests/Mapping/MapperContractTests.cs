// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.StructuredFieldValues.Mapping;

public class MapperContractTests
{
    [Fact]
    public void RequiredReferenceMember_RejectsAbsentAndNull()
    {
        var mapper = StructuredFieldMapper<Model>.Dictionary(b =>
            b.Member("text", x => x.Text, presence: MappingPresence.Required));

        Should.Throw<StructuredFieldParseException>(() => mapper.Parse(""));
        mapper.TryParse("", out var missing).ShouldBeFalse();
        missing.ShouldBeNull();
        Should.Throw<InvalidOperationException>(() => mapper.Serialize(new Model { Text = null }));
        mapper.Parse("text=\"present\"").Text.ShouldBe("present");
        mapper.Serialize(new Model { Text = "present" }).ShouldBe("text=\"present\"");
    }

    [Fact]
    public void RequiredReferenceParameter_RejectsAbsentAndNull()
    {
        var mapper = StructuredFieldMapper<Model>.Item(b => b
            .Value(x => x.Number)
            .Parameter("text", x => x.Text, presence: MappingPresence.Required));

        Should.Throw<StructuredFieldParseException>(() => mapper.Parse("1"));
        mapper.TryParse("1", out _).ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => mapper.Serialize(new Model { Text = null }));
        mapper.Parse("1;text=\"present\"").Text.ShouldBe("present");
        mapper.Serialize(new Model { Number = 1, Text = "present" }).ShouldBe("1;text=\"present\"");
    }

    [Fact]
    public void OptionalValueMember_PreservesInitializerButDoesNotPreserveAbsence()
    {
        var mapper = StructuredFieldMapper<Model>.Dictionary(b =>
            b.Member("n", x => x.Number, presence: MappingPresence.Optional));

        mapper.Parse("").Number.ShouldBe(7);
        mapper.TryParse("", out var value).ShouldBeTrue();
        value.ShouldNotBeNull();
        mapper.Serialize(value).ShouldBe("n=7");
        mapper.Serialize(new Model { Number = 0 }).ShouldBe("n=0");
    }

    [Fact]
    public void OptionalValueParameter_PreservesInitializerButDoesNotPreserveAbsence()
    {
        var mapper = StructuredFieldMapper<Model>.Item(b => b
            .Value(x => x.Text)
            .Parameter("n", x => x.Number, presence: MappingPresence.Optional));

        mapper.TryParse("\"value\"", out var value).ShouldBeTrue();
        value.ShouldNotBeNull();
        value.Number.ShouldBe(7);
        mapper.Serialize(value).ShouldBe("\"value\";n=7");
    }

    [Fact]
    public void DefaultPresence_RequiresNonNullableValuesOnly()
    {
        var dictionary = StructuredFieldMapper<Model>.Dictionary(b => b
            .Member("n", x => x.Number)
            .Member("text", x => x.Text)
            .Member("optional", x => x.Optional));
        dictionary.TryParse("", out _).ShouldBeFalse();
        var parsed = dictionary.Parse("n=3");
        parsed.Text.ShouldBe("initial");
        parsed.Optional.ShouldBe(11);

        var item = StructuredFieldMapper<Model>.Item(b => b
            .Value(x => x.Text)
            .Parameter("n", x => x.Number));
        item.TryParse("\"value\"", out _).ShouldBeFalse();
        item.Parse("\"value\";n=2").Number.ShouldBe(2);
    }

    [Fact]
    public void OptionalNullProperties_AreOmittedOnWrite()
    {
        var dictionary = StructuredFieldMapper<Model>.Dictionary(b => b
            .Member("text", x => x.Text)
            .Member("optional", x => x.Optional));
        dictionary.Serialize(new Model { Text = null, Optional = null }).ShouldBe("");

        var item = StructuredFieldMapper<Model>.Item(b => b
            .Value(x => x.Number)
            .Parameter("text", x => x.Text)
            .Parameter("optional", x => x.Optional));
        item.Serialize(new Model { Text = null, Optional = null }).ShouldBe("7");
    }

    [Fact]
    public void RequiredNullableValue_OverridesOptionalDefault()
    {
        var mapper = StructuredFieldMapper<Model>.Dictionary(b =>
            b.Member("n", x => x.Optional, presence: MappingPresence.Required));
        mapper.TryParse("", out _).ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => mapper.Serialize(new Model { Optional = null }));
        mapper.Serialize(new Model { Optional = 0 }).ShouldBe("n=0");
    }

    [Fact]
    public void RequiredPrimitiveInnerList_RejectsAbsentAndNullButAcceptsEmpty()
    {
        var mapper = StructuredFieldMapper<Model>.Dictionary(b =>
            b.InnerList("values", x => x.Values, presence: MappingPresence.Required));

        mapper.TryParse("", out _).ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => mapper.Serialize(new Model { Values = null! }));
        mapper.Parse("values=()").Values.ShouldBeEmpty();
        mapper.Serialize(new Model()).ShouldBe("values=()");
    }

    [Fact]
    public void RequiredNestedInnerList_RejectsAbsentAndNullButAcceptsEmpty()
    {
        var itemMapper = StructuredFieldMapper<Model>.Item(b => b.Value(x => x.Number));
        var mapper = StructuredFieldMapper<Model>.Dictionary(b =>
            b.InnerList("children", x => x.Children, itemMapper, presence: MappingPresence.Required));

        mapper.TryParse("", out _).ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => mapper.Serialize(new Model { Children = null! }));
        mapper.Parse("children=()").Children.ShouldBeEmpty();
        mapper.Serialize(new Model()).ShouldBe("children=()");
    }

    [Fact]
    public void OptionalInnerLists_OmitNullAndPreserveInitializersWhenAbsent()
    {
        var mapper = StructuredFieldMapper<Model>.Dictionary(b => b.InnerList("values", x => x.Values));
        mapper.Serialize(new Model { Values = null! }).ShouldBe("");
        mapper.Parse("").Values.ShouldBeEmpty();
    }

    [Fact]
    public void Item_RequiresConfiguredValue()
    {
        Should.Throw<InvalidOperationException>(() => StructuredFieldMapper<Model>.Item(_ => { }));
        Should.Throw<InvalidOperationException>(() =>
            StructuredFieldMapper<Model>.Item(b => b.Parameter("n", x => x.Number)));
    }

    [Fact]
    public void Item_NullReferenceOrNullableValueNeverManufacturesBoolean()
    {
        var reference = StructuredFieldMapper<Model>.Item(b => b.Value(x => x.Text));
        var nullable = StructuredFieldMapper<Model>.Item(b => b.Value(x => x.Optional));

        Should.Throw<InvalidOperationException>(() => reference.Serialize(new Model { Text = null }));
        Should.Throw<InvalidOperationException>(() => nullable.Serialize(new Model { Optional = null }));
        Should.Throw<ArgumentNullException>(() => reference.Serialize(null!));
    }

    [Fact]
    public void ParseAndTryParse_HaveEmptyAndMissingInputParity()
    {
        var dictionary = StructuredFieldMapper<Model>.Dictionary(b => b.Member("text", x => x.Text));
        var list = StructuredFieldMapper<Model>.List(b => b.Elements(x => x.Values));
        var item = StructuredFieldMapper<Model>.Item(b => b.Value(x => x.Number));
        foreach (var input in new[] { "", "   " })
        {
            dictionary.TryParse(input, out var dict).ShouldBeTrue();
            dict.ShouldNotBeNull();
            dictionary.Parse(input).Text.ShouldBe(dict.Text);
            list.TryParse(input, out var values).ShouldBeTrue();
            values.ShouldNotBeNull();
            values.Values.ShouldBeEmpty();
            list.Parse(input).Values.ShouldBeEmpty();
            item.TryParse(input, out _).ShouldBeFalse();
            Should.Throw<StructuredFieldParseException>(() => item.Parse(input));
        }

        foreach (var mapper in new[] { dictionary, list, item })
        {
            mapper.TryParse(null, out var value).ShouldBeFalse();
            value.ShouldBeNull();
            Should.Throw<ArgumentNullException>(() => mapper.Parse(null!));
        }
    }

    [Fact]
    public void RetainedDictionaryBuilder_CannotChangeSnapshot()
    {
        DictionaryBuilder<Model>? retained = null;
        var mapper = StructuredFieldMapper<Model>.Dictionary(b =>
        {
            retained = b;
            b.Member("optional", x => x.Optional);
        });
        retained!.Member("text", x => x.Text, presence: MappingPresence.Required);

        mapper.TryParse("", out _).ShouldBeTrue();
        mapper.Parse("text=\"ignored\"").Text.ShouldBe("initial");
        mapper.Serialize(new Model()).ShouldBe("optional=11");
    }

    [Fact]
    public void RetainedItemBuilder_CannotChangeSnapshot()
    {
        ItemBuilder<Model>? retained = null;
        var mapper = StructuredFieldMapper<Model>.Item(b =>
        {
            retained = b;
            b.Value(x => x.Number);
        });
        retained!.Parameter("text", x => x.Text, presence: MappingPresence.Required);

        mapper.TryParse("3", out _).ShouldBeTrue();
        mapper.Parse("3;text=\"ignored\"").Text.ShouldBe("initial");
        mapper.Serialize(new Model()).ShouldBe("7");
    }

    [Fact]
    public void RetainedListBuilder_CannotReplaceConfiguration()
    {
        ListBuilder<Model>? retained = null;
        var mapper = StructuredFieldMapper<Model>.List(b =>
        {
            retained = b;
            b.Elements(x => x.Values);
        });
        Should.Throw<InvalidOperationException>(() => retained!.Elements(x => x.Values, type: ItemType.Token));
        mapper.Serialize(mapper.Parse("\"value\"")).ShouldBe("\"value\"");
    }

    [Fact]
    public void ConcurrentMapperReuse_UsesIndependentModelsAndParameters()
    {
        var item = StructuredFieldMapper<Model>.Item(b => b
            .Value(x => x.Flag)
            .Parameter("n", x => x.Number));
        var list = StructuredFieldMapper<Model>.List(b => b.Elements(x => x.Children, item));

        Parallel.For(0, 128, index =>
        {
            var wire = $"?1;n={index}, ?0;n={index + 1}";
            var parsed = list.Parse(wire);
            list.Serialize(parsed).ShouldBe(wire);
            parsed.Children[0].Number = -1;
            list.Parse(wire).Children[0].Number.ShouldBe(index);
        });
    }

    [Fact]
    public void NestedRegistration_RejectsNonItemMappersImmediately()
    {
        var dictionary = StructuredFieldMapper<Model>.Dictionary(_ => { });
        var list = StructuredFieldMapper<Model>.List(b => b.Elements(x => x.Values));
        foreach (var invalidMapper in new[] { dictionary, list })
        {
            Should.Throw<InvalidOperationException>(() =>
                new ListBuilder<Model>().Elements(x => x.Children, invalidMapper));
            Should.Throw<InvalidOperationException>(() =>
                new DictionaryBuilder<Model>().InnerList("children", x => x.Children, invalidMapper));
        }
    }

    [Fact]
    public void InvalidPropertyExpressions_AreRejectedDuringConfiguration()
    {
        var captured = new Model();
        var builder = new DictionaryBuilder<Model>();

        Should.Throw<ArgumentException>(() => builder.Member("nested", x => x.Child.Number));
        Should.Throw<ArgumentException>(() => builder.Member("captured", x => captured.Number));
        Should.Throw<ArgumentException>(() => builder.Member("static", x => Model.StaticNumber));
        Should.Throw<ArgumentException>(() => builder.Member("field", x => x.Field));
        Should.Throw<ArgumentException>(() => builder.Member("converted", x => (long)x.Number));
        Should.Throw<ArgumentException>(() => builder.Member<object>("boxed", x => x.Number));
        Should.Throw<ArgumentException>(() => builder.Member("readonly", x => x.ReadOnly));
        Should.Throw<ArgumentException>(() => builder.Member("indexer", x => x[0]));
        Should.Throw<ArgumentException>(() => new ItemBuilder<Model>().Value(x => x.Child.Number));
        Should.Throw<ArgumentException>(() => new ItemBuilder<Model>().Parameter("n", x => captured.Number));
        Should.Throw<ArgumentException>(() => new ListBuilder<Model>().Elements(x => captured.Values));
    }

    [Fact]
    public void InitSetter_IsSupported()
    {
        var mapper = StructuredFieldMapper<PriorityHeader>.Dictionary(b => b.Member("u", x => x.Urgency));
        mapper.Parse("u=4").Urgency.ShouldBe(4);
    }

    [Fact]
    public void ExistingPrivateSetterSupport_IsPreserved()
    {
        var mapper = StructuredFieldMapper<Model>.Dictionary(b => b.Member("n", x => x.PrivateSetter));
        mapper.Parse("n=4").PrivateSetter.ShouldBe(4);
    }

    [Fact]
    public void PropertyExceptions_AreNotSwallowed()
    {
        var mapper = StructuredFieldMapper<ThrowingModel>.Dictionary(b => b.Member("n", x => x.Number));
        Should.Throw<AccessorException>(() => mapper.Parse("n=1"));
        Should.Throw<AccessorException>(() => mapper.TryParse("n=1", out _));
        Should.Throw<AccessorException>(() => mapper.Serialize(new ThrowingModel()));
    }

    [Fact]
    public void List_RequiresElementsConfiguration()
    {
        Should.Throw<InvalidOperationException>(() => StructuredFieldMapper<Model>.List(_ => { }));
    }

    [Fact]
    public void NullPrimitiveCollectionElements_AreRejectedInBothListShapes()
    {
        var list = StructuredFieldMapper<Model>.List(b => b.Elements(x => x.Values));
        var dictionary = StructuredFieldMapper<Model>.Dictionary(b => b.InnerList("values", x => x.Values));
        var value = new Model { Values = ["first", null!] };

        Should.Throw<InvalidOperationException>(() => list.Serialize(value));
        Should.Throw<InvalidOperationException>(() => dictionary.Serialize(value));
        list.Serialize(new Model { Values = null! }).ShouldBe("");
    }

    [Fact]
    public void NullNestedCollectionElements_AreRejectedInBothListShapes()
    {
        var item = StructuredFieldMapper<Model>.Item(b => b.Value(x => x.Number));
        var list = StructuredFieldMapper<Model>.List(b => b.Elements(x => x.Children, item));
        var dictionary = StructuredFieldMapper<Model>.Dictionary(b => b.InnerList("children", x => x.Children, item));
        var value = new Model { Children = [new Model(), null!] };

        Should.Throw<InvalidOperationException>(() => list.Serialize(value));
        Should.Throw<InvalidOperationException>(() => dictionary.Serialize(value));
        list.Serialize(new Model { Children = null! }).ShouldBe("");
        dictionary.Serialize(new Model { Children = null! }).ShouldBe("");
    }

    [Fact]
    public void UnknownMembersAndParameters_AreIgnoredAsProjection()
    {
        var dictionary = StructuredFieldMapper<Model>.Dictionary(b => b.Member("n", x => x.Number));
        dictionary.Serialize(dictionary.Parse("unknown=(a b);flag, n=3;extra")).ShouldBe("n=3");

        var item = StructuredFieldMapper<Model>.Item(b => b.Value(x => x.Number));
        item.Serialize(item.Parse("3;unknown")).ShouldBe("3");
    }

    [Fact]
    public void DuplicateParametersAndInvalidPresence_AreRejected()
    {
        Should.Throw<ArgumentException>(() => StructuredFieldMapper<Model>.Item(b => b
            .Value(x => x.Number)
            .Parameter("n", x => x.Number)
            .Parameter("n", x => x.Optional)));
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new DictionaryBuilder<Model>().Member("n", x => x.Number, presence: (MappingPresence)42));
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new ItemBuilder<Model>().Parameter("n", x => x.Number, presence: (MappingPresence)42));
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new DictionaryBuilder<Model>().InnerList("values", x => x.Values, presence: (MappingPresence)42));
    }

    public class Model
    {
        public int Number { get; set; } = 7;
        public int? Optional { get; set; } = 11;
        public bool Flag { get; set; }
        public string? Text { get; set; } = "initial";
        public IReadOnlyList<string> Values { get; set; } = [];
        public IReadOnlyList<Model> Children { get; set; } = [];
        public Model Child { get; set; } = null!;
        public static int StaticNumber { get; set; }
        public int Field;
        public int ReadOnly => 1;
        public int PrivateSetter { get; private set; }
        public int this[int index] { get => index; set { } }
    }

    public class ThrowingModel
    {
        public int Number
        {
            get => throw new AccessorException();
            set => throw new AccessorException();
        }
    }

    public class AccessorException : Exception;
}
