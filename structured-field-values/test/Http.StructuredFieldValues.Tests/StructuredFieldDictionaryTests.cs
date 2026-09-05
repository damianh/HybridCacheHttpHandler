// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.StructuredFieldValues;

public class StructuredFieldDictionaryTests
{
    [Fact]
    public void Constructor_WithMembers_Success()
    {
        var members = new[]
        {
            new KeyValuePair<string, StructuredFieldMember>("a", StructuredFieldMember.FromItem(new IntegerItem(1))),
            new KeyValuePair<string, StructuredFieldMember>("b", StructuredFieldMember.FromItem(new IntegerItem(2)))
        };

        var dict = new StructuredFieldDictionary(members);
        
        dict.Count.ShouldBe(2);
        dict["a"].Item.Value.ShouldBeOfType<IntegerItem>();
        dict["b"].Item.Value.ShouldBeOfType<IntegerItem>();
    }

    [Fact]
    public void Constructor_WithNull_ThrowsArgumentNullException() => Should.Throw<ArgumentNullException>(() => new StructuredFieldDictionary(null!));

    [Fact]
    public void Add_Member_Success()
    {
        var dict = new StructuredFieldDictionary();
        var member = StructuredFieldMember.FromItem(new IntegerItem(42));
        
        dict.Add("test", member);
        
        dict.Count.ShouldBe(1);
        dict["test"].ShouldBe(member);
    }

    [Fact]
    public void Add_Item_Success()
    {
        var dict = new StructuredFieldDictionary();
        var item = new IntegerItem(42);
        
        dict.Add("test", item);
        
        dict.Count.ShouldBe(1);
        dict["test"].Item.Value.ShouldBe(item);
    }

    [Fact]
    public void Add_InnerList_Success()
    {
        var dict = new StructuredFieldDictionary();
        var innerList = new InnerList();
        innerList.Add(new IntegerItem(1));
        
        dict.Add("test", innerList);
        
        dict.Count.ShouldBe(1);
        dict["test"].InnerList.ShouldBe(innerList);
    }

    [Fact]
    public void Add_NullMember_ThrowsArgumentNullException()
    {
        var dict = new StructuredFieldDictionary();
        Should.Throw<ArgumentNullException>(() => dict.Add("test", (StructuredFieldMember)null!));
    }

    [Fact]
    public void Add_NullItem_ThrowsArgumentNullException()
    {
        var dict = new StructuredFieldDictionary();
        Should.Throw<ArgumentNullException>(() => dict.Add("test", (StructuredFieldItem)null!));
    }

    [Fact]
    public void Add_NullInnerList_ThrowsArgumentNullException()
    {
        var dict = new StructuredFieldDictionary();
        Should.Throw<ArgumentNullException>(() => dict.Add("test", (InnerList)null!));
    }

    [Fact]
    public void Add_DuplicateKey_ThrowsArgumentException()
    {
        var dict = new StructuredFieldDictionary { { "test", new IntegerItem(1) } };

        Should.Throw<ArgumentException>(() => dict.Add("test", new IntegerItem(2)));
    }

    [Fact]
    public void Add_InvalidKey_ThrowsArgumentException()
    {
        var dict = new StructuredFieldDictionary();
        var exception = Should.Throw<ArgumentException>(() => dict.Add("invalid key", new IntegerItem(1)));
        exception.Message.ShouldContain("Dictionary key 'invalid key' is not a valid RFC 9651 key.");
    }

    [Fact]
    public void Add_EmptyKey_ThrowsArgumentException()
    {
        var dict = new StructuredFieldDictionary();
        Should.Throw<ArgumentException>(() => dict.Add("", new IntegerItem(1)));
    }

    [Fact]
    public void Indexer_Set_NewKey_Success()
    {
        var dict = new StructuredFieldDictionary();
        var member = StructuredFieldMember.FromItem(new IntegerItem(42));
        
        dict["newkey"] = member;
        
        dict.Count.ShouldBe(1);
        dict["newkey"].ShouldBe(member);
    }

    [Fact]
    public void Indexer_Set_ExistingKey_UpdatesValue()
    {
        var dict = new StructuredFieldDictionary();
        dict["test"] = StructuredFieldMember.FromItem(new IntegerItem(1));
        dict["test"] = StructuredFieldMember.FromItem(new IntegerItem(2));
        
        dict.Count.ShouldBe(1);
        ((IntegerItem)dict["test"].Item.Value).LongValue.ShouldBe(2);
    }

    [Fact]
    public void Indexer_Get_NonExistentKey_ThrowsKeyNotFoundException()
    {
        var dict = new StructuredFieldDictionary();
        Should.Throw<KeyNotFoundException>(() => { _ = dict["missing"]; });
    }

    [Fact]
    public void TryGetValue_ExistingKey_ReturnsTrue()
    {
        var dict = new StructuredFieldDictionary();
        var member = StructuredFieldMember.FromItem(new IntegerItem(42));
        dict.Add("test", member);
        
        var result = dict.TryGetValue("test", out var value);
        
        result.ShouldBeTrue();
        value.ShouldBe(member);
    }

    [Fact]
    public void TryGetValue_NonExistentKey_ReturnsFalse()
    {
        var dict = new StructuredFieldDictionary();
        
        var result = dict.TryGetValue("missing", out var value);
        
        result.ShouldBeFalse();
        value.ShouldBeNull();
    }

    [Fact]
    public void ContainsKey_ExistingKey_ReturnsTrue()
    {
        var dict = new StructuredFieldDictionary { { "test", new IntegerItem(42) } };

        dict.ContainsKey("test").ShouldBeTrue();
    }

    [Fact]
    public void ContainsKey_NonExistentKey_ReturnsFalse()
    {
        var dict = new StructuredFieldDictionary();
        dict.ContainsKey("missing").ShouldBeFalse();
    }

    [Fact]
    public void Remove_ExistingKey_ReturnsTrue()
    {
        var dict = new StructuredFieldDictionary { { "test", new IntegerItem(42) } };

        var result = dict.Remove("test");
        
        result.ShouldBeTrue();
        dict.Count.ShouldBe(0);
    }

    [Fact]
    public void Remove_NonExistentKey_ReturnsFalse()
    {
        var dict = new StructuredFieldDictionary();
        
        var result = dict.Remove("missing");
        
        result.ShouldBeFalse();
    }

    [Fact]
    public void Clear_RemovesAllMembers()
    {
        var dict = new StructuredFieldDictionary
        {
            { "a", new IntegerItem(1) },
            { "b", new IntegerItem(2) }
        };

        dict.Clear();
        
        dict.Count.ShouldBe(0);
    }

    [Fact]
    public void GetEnumerator_PreservesInsertionOrder()
    {
        var dict = new StructuredFieldDictionary
        {
            { "c", new IntegerItem(3) },
            { "a", new IntegerItem(1) },
            { "b", new IntegerItem(2) }
        };

        var keys = dict.Select(kvp => kvp.Key).ToList();
        
        keys.ShouldBe(new[] { "c", "a", "b" });
    }

    [Fact]
    public void ToString_EmptyDictionary_IsDiagnostic()
    {
        var dict = new StructuredFieldDictionary();
        dict.ToString().ShouldBe("Dictionary(0 members)");
    }

    [Fact]
    public void ToString_WithMembers_IsDiagnostic()
    {
        var dict = new StructuredFieldDictionary
        {
            { "a", new IntegerItem(1) },
            { "b", new IntegerItem(2) }
        };

        dict.ToString().ShouldBe("Dictionary(2 members)");
    }

    [Fact]
    public void NullMembers_AreRejectedByAllEntryPoints()
    {
        var dictionary = new StructuredFieldDictionary();
        Should.Throw<ArgumentNullException>(() => dictionary.Add("a", (BareItem)null!));
        Should.Throw<ArgumentNullException>(() => dictionary["a"] = null!);
        Should.Throw<ArgumentNullException>(() => new StructuredFieldDictionary(
            [new KeyValuePair<string, StructuredFieldMember>("a", null!)]));
        dictionary.Count.ShouldBe(0);
    }
}
