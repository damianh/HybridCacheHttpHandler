// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.StructuredFieldValues;

public class InnerListTests
{
    [Fact]
    public void Constructor_Empty_Success()
    {
        var innerList = new InnerList();
        
        innerList.Count.ShouldBe(0);
        innerList.Parameters.Count.ShouldBe(0);
    }

    [Fact]
    public void Constructor_WithItems_Success()
    {
        var items = new StructuredFieldItem[]
        {
            new IntegerItem(1),
            new IntegerItem(2),
            new IntegerItem(3)
        };

        var innerList = new InnerList(items);
        
        innerList.Count.ShouldBe(3);
        innerList[0].Value.ShouldBeOfType<IntegerItem>();
        innerList[1].Value.ShouldBeOfType<IntegerItem>();
        innerList[2].Value.ShouldBeOfType<IntegerItem>();
    }

    [Fact]
    public void Constructor_WithItemsAndParameters_Success()
    {
        var items = new StructuredFieldItem[] { new IntegerItem(1) };
        var parameters = new Parameters { { "test", new IntegerItem(42) } };

        var innerList = new InnerList(items, parameters);
        
        innerList.Count.ShouldBe(1);
        innerList.Parameters.Count.ShouldBe(1);
        innerList.Parameters["test"].ShouldBeOfType<IntegerItem>();
    }

    [Fact]
    public void Constructor_WithNullItems_ThrowsArgumentNullException() => Should.Throw<ArgumentNullException>(() => new InnerList(null!));

    [Fact]
    public void Constructor_WithNullParameters_CreatesOwnedEmptyParameters()
    {
        var items = new StructuredFieldItem[] { new IntegerItem(1) };
        new InnerList(items, null).Parameters.Count.ShouldBe(0);
    }

    [Fact]
    public void Add_ValidItem_Success()
    {
        var innerList = new InnerList();
        innerList.Add(new IntegerItem(42));
        
        innerList.Count.ShouldBe(1);
        innerList[0].Value.ShouldBeOfType<IntegerItem>();
    }

    [Fact]
    public void Add_NullItem_ThrowsArgumentNullException()
    {
        var innerList = new InnerList();
        Should.Throw<ArgumentNullException>(() => innerList.Add((StructuredFieldItem)null!));
        Should.Throw<ArgumentNullException>(() => innerList.Add((BareItem)null!));
    }

    [Fact]
    public void AddRange_ValidItems_Success()
    {
        var innerList = new InnerList();
        var items = new StructuredFieldItem[]
        {
            new IntegerItem(1),
            new IntegerItem(2)
        };

        innerList.AddRange(items);
        
        innerList.Count.ShouldBe(2);
    }

    [Fact]
    public void AddRange_NullItems_ThrowsArgumentNullException()
    {
        var innerList = new InnerList();
        Should.Throw<ArgumentNullException>(() => innerList.AddRange(null!));
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        var innerList = new InnerList();
        innerList.Add(new IntegerItem(1));
        innerList.Add(new IntegerItem(2));
        
        innerList.Clear();
        
        innerList.Count.ShouldBe(0);
    }

    [Fact]
    public void Indexer_ValidIndex_ReturnsItem()
    {
        var innerList = new InnerList();
        innerList.Add(new IntegerItem(42));
        
        var item = innerList[0];
        
        item.Value.ShouldBeOfType<IntegerItem>();
        ((IntegerItem)item.Value).LongValue.ShouldBe(42);
    }

    [Fact]
    public void Items_ReturnsReadOnlyList()
    {
        var innerList = new InnerList();
        innerList.Add(new IntegerItem(1));
        
        var items = innerList.Items;
        
        items.ShouldBeOfType<System.Collections.ObjectModel.ReadOnlyCollection<StructuredFieldItem>>();
        items.Count.ShouldBe(1);
    }

    [Fact]
    public void ToString_EmptyList_IsDiagnostic()
    {
        var innerList = new InnerList();
        innerList.ToString().ShouldBe("InnerList(0 items; 0 parameters)");
    }

    [Fact]
    public void ToString_WithItems_IsDiagnostic()
    {
        var innerList = new InnerList();
        innerList.Add(new IntegerItem(1));
        innerList.Add(new IntegerItem(2));
        
        innerList.ToString().ShouldBe("InnerList(2 items; 0 parameters)");
    }

    [Fact]
    public void ToString_WithParameters_IncludesParameters()
    {
        var innerList = new InnerList();
        innerList.Add(new IntegerItem(1));
        innerList.Parameters.Add("test", new IntegerItem(42));
        
        innerList.ToString().ShouldBe("InnerList(1 items; 1 parameters)");
    }

    [Fact]
    public void Serialize_WithTrueParameter_IncludesFlag()
    {
        var innerList = new InnerList();
        innerList.Add(new IntegerItem(1));
        innerList.Parameters.Add("flag");
        
        StructuredFieldSerializer.SerializeInnerList(innerList).ShouldBe("(1);flag");
    }

    [Fact]
    public void Constructor_CopiesParameters()
    {
        var parameters = new Parameters { { "test", new IntegerItem(42) } };

        var innerList = new InnerList([], parameters);
        parameters.Clear();
        
        innerList.Parameters["test"].ShouldBeOfType<IntegerItem>();
    }

    [Fact]
    public void NullElements_AreRejectedAtomically()
    {
        var list = new InnerList([new IntegerItem(1)]);
        Should.Throw<ArgumentNullException>(() => new InnerList([new IntegerItem(2), null!]));
        Should.Throw<ArgumentNullException>(() => list.AddRange([new IntegerItem(2), null!]));
        Should.Throw<ArgumentNullException>(() => list[0] = null!);
        list.Count.ShouldBe(1);
        list[0].Value.ShouldBe(new IntegerItem(1));
    }
}
