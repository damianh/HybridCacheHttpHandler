// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.StructuredFieldValues;

public class UnifiedSerializerTests
{
    [Fact]
    public void EveryWriterEntryPoint_UsesTheSameCanonicalRules()
    {
        BareItem bare = new DisplayStringItem("a\"%\\🚀");
        var parameters = new Parameters { { "flag", BooleanItem.True }, { "date", new DateItem(7) } };
        var item = new StructuredFieldItem(bare, parameters);
        var member = StructuredFieldMember.FromItem(item);
        var inner = new InnerList([item], parameters);
        var innerMember = StructuredFieldMember.FromInnerList(inner);
        var list = new StructuredFieldList([member, innerMember]);
        var dictionary = new StructuredFieldDictionary { { "a", member }, { "b", innerMember } };
        const string bareWire = "%\"a%22%25\\%f0%9f%9a%80\"";
        const string itemWire = bareWire + ";flag;date=@7";
        const string innerWire = "(" + itemWire + ");flag;date=@7";

        StructuredFieldSerializer.SerializeBareItem(bare).ShouldBe(bareWire);
        StructuredFieldSerializer.SerializeItem(item).ShouldBe(itemWire);
        StructuredFieldSerializer.SerializeMember(member).ShouldBe(itemWire);
        StructuredFieldSerializer.SerializeMember(innerMember).ShouldBe(innerWire);
        StructuredFieldSerializer.SerializeInnerList(inner).ShouldBe(innerWire);
        StructuredFieldSerializer.SerializeList(list).ShouldBe(itemWire + ", " + innerWire);
        StructuredFieldSerializer.SerializeDictionary(dictionary).ShouldBe("a=" + itemWire + ", b=" + innerWire);
        item.ToString().ShouldNotBe(itemWire);
        inner.ToString().ShouldNotBe(innerWire);
        bare.ToString().ShouldNotBe(bareWire);
    }

    [Fact]
    public void TrueMembersAndParameters_AreCanonicalizedWithoutLosingParameterOwnership()
    {
        var item = new StructuredFieldItem(BooleanItem.True, new Parameters
        {
            { "yes", BooleanItem.True },
            { "no", BooleanItem.False },
            { "text", new StringItem("quoted") }
        });
        var dictionary = new StructuredFieldDictionary { { "a", item }, { "b", BooleanItem.False } };
        StructuredFieldSerializer.SerializeDictionary(dictionary).ShouldBe("a;yes;no=?0;text=\"quoted\", b=?0");
        StructuredFieldSerializer.SerializeMember(StructuredFieldMember.FromItem(item))
            .ShouldBe("?1;yes;no=?0;text=\"quoted\"");
    }

    [Fact]
    public void EveryWriterEntryPoint_RejectsNull()
    {
        Should.Throw<ArgumentNullException>(() => StructuredFieldSerializer.SerializeBareItem(null!));
        Should.Throw<ArgumentNullException>(() => StructuredFieldSerializer.SerializeItem(null!));
        Should.Throw<ArgumentNullException>(() => StructuredFieldSerializer.SerializeMember(null!));
        Should.Throw<ArgumentNullException>(() => StructuredFieldSerializer.SerializeInnerList(null!));
        Should.Throw<ArgumentNullException>(() => StructuredFieldSerializer.SerializeList(null!));
        Should.Throw<ArgumentNullException>(() => StructuredFieldSerializer.SerializeDictionary(null!));
    }
}
