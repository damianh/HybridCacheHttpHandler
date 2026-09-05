// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.StructuredFieldValues;

public class StructuredFieldMemberTests
{
    [Fact]
    public void ItemMember_ExposesSameNodeAndParameters()
    {
        var item = new StructuredFieldItem(new IntegerItem(42), new Parameters { { "flag", BooleanItem.True } });
        var member = StructuredFieldMember.FromItem(item);
        member.IsItem.ShouldBeTrue();
        member.IsInnerList.ShouldBeFalse();
        member.Item.ShouldBeSameAs(item);
        member.Parameters.ShouldBeSameAs(item.Parameters);
        member.TryGetItem(out var found).ShouldBeTrue();
        found.ShouldBeSameAs(item);
        member.TryGetInnerList(out var missing).ShouldBeFalse();
        missing.ShouldBeNull();
        Should.Throw<InvalidOperationException>(() => member.InnerList);
        member.ToString().ShouldBe(item.ToString());
    }

    [Fact]
    public void InnerListMember_ExposesSameNodeAndParameters()
    {
        var inner = new InnerList([new IntegerItem(1)], new Parameters { { "flag", BooleanItem.True } });
        var member = StructuredFieldMember.FromInnerList(inner);
        member.IsItem.ShouldBeFalse();
        member.IsInnerList.ShouldBeTrue();
        member.InnerList.ShouldBeSameAs(inner);
        member.Parameters.ShouldBeSameAs(inner.Parameters);
        member.TryGetInnerList(out var found).ShouldBeTrue();
        found.ShouldBeSameAs(inner);
        member.TryGetItem(out var missing).ShouldBeFalse();
        missing.ShouldBeNull();
        Should.Throw<InvalidOperationException>(() => member.Item);
        member.ToString().ShouldBe(inner.ToString());
    }

    [Fact]
    public void FactoryAndConversions_RejectNull()
    {
        Should.Throw<ArgumentNullException>(() => StructuredFieldMember.FromItem((StructuredFieldItem)null!));
        Should.Throw<ArgumentNullException>(() => StructuredFieldMember.FromItem((BareItem)null!));
        Should.Throw<ArgumentNullException>(() => StructuredFieldMember.FromInnerList(null!));
        Should.Throw<ArgumentNullException>(() => (StructuredFieldMember)(BareItem)null!);
        Should.Throw<ArgumentNullException>(() => (StructuredFieldMember)(StructuredFieldItem)null!);
        Should.Throw<ArgumentNullException>(() => (StructuredFieldMember)(InnerList)null!);
    }

    [Fact]
    public void ImplicitConversions_SupportAllMemberShapes()
    {
        BareItem bare = new IntegerItem(1);
        StructuredFieldItem item = bare;
        StructuredFieldMember bareMember = bare;
        StructuredFieldMember itemMember = item;
        var inner = new InnerList([item]);
        StructuredFieldMember innerMember = inner;
        bareMember.Item.Value.ShouldBeSameAs(bare);
        itemMember.Item.ShouldBeSameAs(item);
        innerMember.InnerList.ShouldBeSameAs(inner);
    }

    [Fact]
    public void Parameters_FollowExplicitlySharedNodes()
    {
        var item = new StructuredFieldItem(BooleanItem.True);
        var list = new StructuredFieldList();
        var dictionary = new StructuredFieldDictionary();
        list.Add(item);
        dictionary.Add("a", item);
        list[0].Parameters.Add("flag");
        dictionary["a"].Parameters["flag"].ShouldBeSameAs(BooleanItem.True);
        item.Value = new DateItem(42);
        dictionary["a"].Item.Type.ShouldBe(ItemType.Date);
        list[0].Item.ShouldBeSameAs(dictionary["a"].Item);
    }
}
