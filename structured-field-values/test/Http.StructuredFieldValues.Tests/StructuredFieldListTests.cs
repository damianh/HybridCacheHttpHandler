// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.StructuredFieldValues;

public class StructuredFieldListTests
{
    [Fact]
    public void Constructor_WithMembers_Success()
    {
        var members = new[]
        {
            StructuredFieldMember.FromItem(new IntegerItem(1)),
            StructuredFieldMember.FromItem(new IntegerItem(2))
        };

        var list = new StructuredFieldList(members);
        
        list.Count.ShouldBe(2);
        list[0].Item.Value.ShouldBeOfType<IntegerItem>();
        list[1].Item.Value.ShouldBeOfType<IntegerItem>();
    }

    [Fact]
    public void Constructor_WithNull_ThrowsArgumentNullException() => Should.Throw<ArgumentNullException>(() => new StructuredFieldList(null!));

    [Fact]
    public void Add_Member_Success()
    {
        var list = new StructuredFieldList();
        var member = StructuredFieldMember.FromItem(new IntegerItem(42));
        
        list.Add(member);
        
        list.Count.ShouldBe(1);
        list[0].ShouldBe(member);
    }

    [Fact]
    public void Add_Item_Success()
    {
        var list = new StructuredFieldList();
        var item = new IntegerItem(42);
        
        list.Add(item);
        
        list.Count.ShouldBe(1);
        list[0].Item.Value.ShouldBe(item);
    }

    [Fact]
    public void Add_InnerList_Success()
    {
        var list = new StructuredFieldList();
        var innerList = new InnerList();
        innerList.Add(new IntegerItem(1));
        
        list.Add(innerList);
        
        list.Count.ShouldBe(1);
        list[0].InnerList.ShouldBe(innerList);
    }

    [Fact]
    public void Add_NullMember_ThrowsArgumentNullException()
    {
        var list = new StructuredFieldList();
        Should.Throw<ArgumentNullException>(() => list.Add((StructuredFieldMember)null!));
    }

    [Fact]
    public void Add_NullItem_ThrowsArgumentNullException()
    {
        var list = new StructuredFieldList();
        Should.Throw<ArgumentNullException>(() => list.Add((StructuredFieldItem)null!));
    }

    [Fact]
    public void Add_NullInnerList_ThrowsArgumentNullException()
    {
        var list = new StructuredFieldList();
        Should.Throw<ArgumentNullException>(() => list.Add((InnerList)null!));
    }

    [Fact]
    public void AddRange_ValidMembers_Success()
    {
        var list = new StructuredFieldList();
        var members = new[]
        {
            StructuredFieldMember.FromItem(new IntegerItem(1)),
            StructuredFieldMember.FromItem(new IntegerItem(2))
        };

        list.AddRange(members);
        
        list.Count.ShouldBe(2);
    }

    [Fact]
    public void AddRange_NullMembers_ThrowsArgumentNullException()
    {
        var list = new StructuredFieldList();
        Should.Throw<ArgumentNullException>(() => list.AddRange(null!));
    }

    [Fact]
    public void Clear_RemovesAllMembers()
    {
        var list = new StructuredFieldList();
        list.Add(new IntegerItem(1));
        list.Add(new IntegerItem(2));
        
        list.Clear();
        
        list.Count.ShouldBe(0);
    }

    [Fact]
    public void Indexer_ValidIndex_ReturnsMember()
    {
        var list = new StructuredFieldList();
        var item = new IntegerItem(42);
        list.Add(item);
        
        var member = list[0];
        
        member.IsItem.ShouldBeTrue();
        member.Item.Value.ShouldBe(item);
    }

    [Fact]
    public void ToString_EmptyList_IsDiagnostic()
    {
        var list = new StructuredFieldList();
        list.ToString().ShouldBe("List(0 members)");
    }

    [Fact]
    public void ToString_WithMembers_IsDiagnostic()
    {
        var list = new StructuredFieldList();
        list.Add(new IntegerItem(1));
        list.Add(new IntegerItem(2));
        
        list.ToString().ShouldBe("List(2 members)");
    }

    [Fact]
    public void ToString_WithInnerList_IsDiagnostic()
    {
        var list = new StructuredFieldList();
        var innerList = new InnerList();
        innerList.Add(new IntegerItem(1));
        innerList.Add(new IntegerItem(2));
        
        list.Add(innerList);
        list.Add(new IntegerItem(3));
        
        list.ToString().ShouldBe("List(2 members)");
    }

    [Fact]
    public void NullElements_AreRejectedAtomically()
    {
        var list = new StructuredFieldList([new IntegerItem(1)]);
        Should.Throw<ArgumentNullException>(() => new StructuredFieldList([new IntegerItem(2), null!]));
        Should.Throw<ArgumentNullException>(() => list.AddRange([new IntegerItem(2), null!]));
        Should.Throw<ArgumentNullException>(() => list.Add((BareItem)null!));
        Should.Throw<ArgumentNullException>(() => list[0] = null!);
        list.Count.ShouldBe(1);
    }
}
