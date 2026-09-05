// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.StructuredFieldValues;

public class ParametersTests
{
    [Fact]
    public void Constructor_WithInitialValues_Success()
    {
        var initial = new[]
        {
            new KeyValuePair<string, BareItem>("a", new IntegerItem(1)),
            new KeyValuePair<string, BareItem>("b", new IntegerItem(2))
        };

        var parameters = new Parameters(initial);
        
        parameters.Count.ShouldBe(2);
        parameters["a"].ShouldBeOfType<IntegerItem>();
        parameters["b"].ShouldBeOfType<IntegerItem>();
    }

    [Fact]
    public void Constructor_WithNull_ThrowsArgumentNullException() => Should.Throw<ArgumentNullException>(() => new Parameters(null!));

    [Fact]
    public void Add_ValidParameter_Success()
    {
        var parameters = new Parameters { { "test", new IntegerItem(42) } };

        parameters.Count.ShouldBe(1);
        parameters["test"].ShouldBeOfType<IntegerItem>();
    }

    [Fact]
    public void Add_Flag_IsNonNullBooleanTrue()
    {
        var parameters = new Parameters();
        parameters.Add("flag");

        parameters.Count.ShouldBe(1);
        parameters["flag"].ShouldBeSameAs(BooleanItem.True);
    }

    [Fact]
    public void NullValues_AreRejectedByAllEntryPoints()
    {
        var parameters = new Parameters();
        Should.Throw<ArgumentNullException>(() => parameters.Add("flag", null!));
        Should.Throw<ArgumentNullException>(() => parameters["flag"] = null!);
        Should.Throw<ArgumentNullException>(() =>
            new Parameters([new KeyValuePair<string, BareItem>("flag", null!)]));
        parameters.Count.ShouldBe(0);
    }

    [Fact]
    public void Replacement_PreservesFirstPosition()
    {
        var parameters = new Parameters { { "a", new IntegerItem(1) }, { "b", new IntegerItem(2) } };
        parameters["a"] = BooleanItem.True;
        parameters.Select(x => x.Key).ShouldBe(["a", "b"]);
        parameters["a"].ShouldBeSameAs(BooleanItem.True);
    }

    [Fact]
    public void Add_DuplicateKey_ThrowsArgumentException()
    {
        var parameters = new Parameters { { "test", new IntegerItem(1) } };

        Should.Throw<ArgumentException>(() => parameters.Add("test", new IntegerItem(2)));
    }

    [Fact]
    public void Add_InvalidKey_ThrowsArgumentException()
    {
        var parameters = new Parameters();
        Should.Throw<ArgumentException>(() => parameters.Add("invalid key", new IntegerItem(1)));
    }

    [Fact]
    public void Add_EmptyKey_ThrowsArgumentException()
    {
        var parameters = new Parameters();
        Should.Throw<ArgumentException>(() => parameters.Add("", new IntegerItem(1)));
    }

    [Fact]
    public void Indexer_Set_NewKey_Success()
    {
        var parameters = new Parameters();
        parameters["newkey"] = new IntegerItem(42);
        
        parameters.Count.ShouldBe(1);
        parameters["newkey"].ShouldBeOfType<IntegerItem>();
    }

    [Fact]
    public void Indexer_Set_ExistingKey_UpdatesValue()
    {
        var parameters = new Parameters();
        parameters["test"] = new IntegerItem(1);
        parameters["test"] = new IntegerItem(2);
        
        parameters.Count.ShouldBe(1);
        ((IntegerItem)parameters["test"]!).LongValue.ShouldBe(2);
    }

    [Fact]
    public void Indexer_Get_NonExistentKey_ThrowsKeyNotFoundException()
    {
        var parameters = new Parameters();
        Should.Throw<KeyNotFoundException>(() => { _ = parameters["missing"]; });
    }

    [Fact]
    public void TryGetValue_ExistingKey_ReturnsTrue()
    {
        var parameters = new Parameters { { "test", new IntegerItem(42) } };

        var result = parameters.TryGetValue("test", out var value);
        
        result.ShouldBeTrue();
        value.ShouldNotBeNull();
        value.ShouldBeOfType<IntegerItem>();
    }

    [Fact]
    public void TryGetValue_NonExistentKey_ReturnsFalse()
    {
        var parameters = new Parameters();
        
        var result = parameters.TryGetValue("missing", out var value);
        
        result.ShouldBeFalse();
        value.ShouldBeNull();
    }

    [Fact]
    public void ContainsKey_ExistingKey_ReturnsTrue()
    {
        var parameters = new Parameters { { "test", new IntegerItem(42) } };

        parameters.ContainsKey("test").ShouldBeTrue();
    }

    [Fact]
    public void ContainsKey_NonExistentKey_ReturnsFalse()
    {
        var parameters = new Parameters();
        parameters.ContainsKey("missing").ShouldBeFalse();
    }

    [Fact]
    public void Remove_ExistingKey_ReturnsTrue()
    {
        var parameters = new Parameters { { "test", new IntegerItem(42) } };

        var result = parameters.Remove("test");
        
        result.ShouldBeTrue();
        parameters.Count.ShouldBe(0);
    }

    [Fact]
    public void Remove_NonExistentKey_ReturnsFalse()
    {
        var parameters = new Parameters();
        
        var result = parameters.Remove("missing");
        
        result.ShouldBeFalse();
    }

    [Fact]
    public void Clear_RemovesAllParameters()
    {
        var parameters = new Parameters
        {
            { "a", new IntegerItem(1) },
            { "b", new IntegerItem(2) }
        };

        parameters.Clear();
        
        parameters.Count.ShouldBe(0);
    }

    [Fact]
    public void GetEnumerator_PreservesInsertionOrder()
    {
        var parameters = new Parameters
        {
            { "c", new IntegerItem(3) },
            { "a", new IntegerItem(1) },
            { "b", new IntegerItem(2) }
        };

        var keys = parameters.Select(kvp => kvp.Key).ToList();
        
        keys.ShouldBe(new[] { "c", "a", "b" });
    }
}
