// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using DamianH.Http.StructuredFieldValues;
using Shouldly;

namespace DamianH.Http.HttpSignatures;

public class ComponentIdentifierTests
{
    [Fact]
    public void Method_StaticProperty_SerializesCorrectly()
    {
        ComponentIdentifier.Method.Serialize().ShouldBe("\"@method\"");
    }

    [Fact]
    public void Authority_StaticProperty_SerializesCorrectly()
    {
        ComponentIdentifier.Authority.Serialize().ShouldBe("\"@authority\"");
    }

    [Fact]
    public void Path_StaticProperty_SerializesCorrectly()
    {
        ComponentIdentifier.Path.Serialize().ShouldBe("\"@path\"");
    }

    [Fact]
    public void Query_StaticProperty_SerializesCorrectly()
    {
        ComponentIdentifier.Query.Serialize().ShouldBe("\"@query\"");
    }

    [Fact]
    public void Scheme_StaticProperty_SerializesCorrectly()
    {
        ComponentIdentifier.Scheme.Serialize().ShouldBe("\"@scheme\"");
    }

    [Fact]
    public void TargetUri_StaticProperty_SerializesCorrectly()
    {
        ComponentIdentifier.TargetUri.Serialize().ShouldBe("\"@target-uri\"");
    }

    [Fact]
    public void RequestTarget_StaticProperty_SerializesCorrectly()
    {
        ComponentIdentifier.RequestTarget.Serialize().ShouldBe("\"@request-target\"");
    }

    [Fact]
    public void Status_StaticProperty_SerializesCorrectly()
    {
        ComponentIdentifier.Status.Serialize().ShouldBe("\"@status\"");
    }

    [Fact]
    public void QueryParam_SerializesWithNameParameter()
    {
        ComponentIdentifier.QueryParam("Pet").Serialize().ShouldBe("\"@query-param\";name=\"Pet\"");
    }

    [Fact]
    public void Field_PlainField_SerializesCorrectly()
    {
        ComponentIdentifier.Field("content-type").Serialize().ShouldBe("\"content-type\"");
    }

    [Fact]
    public void Field_NameNormalizesToLowercase()
    {
        var ci = new ComponentIdentifier("Content-Type");
        ci.Name.ShouldBe("content-type");
    }

    [Fact]
    public void FieldSf_SerializesWithSfParameter()
    {
        ComponentIdentifier.FieldSf("content-digest").Serialize().ShouldBe("\"content-digest\";sf");
    }

    [Fact]
    public void FieldKey_SerializesWithKeyParameter()
    {
        ComponentIdentifier.FieldKey("priority", "u").Serialize().ShouldBe("\"priority\";key=\"u\"");
    }

    [Fact]
    public void FieldBs_SerializesWithBsParameter()
    {
        ComponentIdentifier.FieldBs("x-custom").Serialize().ShouldBe("\"x-custom\";bs");
    }

    [Fact]
    public void ComponentWithReq_SerializesWithReqParameter()
    {
        var ci = new ComponentIdentifier("@method") { Req = true };
        ci.Serialize().ShouldBe("\"@method\";req");
    }

    [Fact]
    public void IsDerived_DerivedComponent_ReturnsTrue()
    {
        ComponentIdentifier.Method.IsDerived.ShouldBeTrue();
    }

    [Fact]
    public void IsDerived_RegularField_ReturnsFalse()
    {
        ComponentIdentifier.Field("content-type").IsDerived.ShouldBeFalse();
    }

    [Fact]
    public void Equals_SameNameNoParams_ReturnsTrue()
    {
        var a = ComponentIdentifier.Method;
        var b = new ComponentIdentifier("@method");
        a.Equals(b).ShouldBeTrue();
    }

    [Fact]
    public void Equals_DifferentName_ReturnsFalse()
    {
        ComponentIdentifier.Method.Equals(ComponentIdentifier.Path).ShouldBeFalse();
    }

    [Fact]
    public void Equals_DifferentParams_ReturnsFalse()
    {
        var a = ComponentIdentifier.Field("priority");
        var b = ComponentIdentifier.FieldSf("priority");
        a.Equals(b).ShouldBeFalse();
    }

    [Fact]
    public void GetHashCode_EqualInstances_SameHashCode()
    {
        var a = ComponentIdentifier.Method;
        var b = new ComponentIdentifier("@method");
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsSameAsSerialize()
    {
        var ci = ComponentIdentifier.QueryParam("foo");
        ci.ToString().ShouldBe(ci.Serialize());
    }

    [Fact]
    public void SfAndReq_BothParams_SerializesInOrder()
    {
        var ci = new ComponentIdentifier("example-dict") { Sf = true, Req = true };
        ci.Serialize().ShouldBe("\"example-dict\";sf;req");
    }

    [Fact]
    public void Parameters_CalledRepeatedly_ReturnsCachedInstance()
    {
        // ComponentIdentifier is immutable, so the computed parameter snapshot should be
        // cached rather than rebuilt (and reallocated) on every access.
        var ci = new ComponentIdentifier("example-dict") { Sf = true, Req = true };

        var first = ci.Parameters;
        var second = ci.Parameters;

        ReferenceEquals(first, second).ShouldBeTrue();
        first.ShouldNotBeOfType<KeyValuePair<string, BareItem>[]>();
    }

    [Fact]
    public void Parameters_FromWire_CalledRepeatedly_ReturnsCachedInstancePreservingOrder()
    {
        var wireParameters = new Parameters();
        wireParameters.Add("req", BooleanItem.True);
        wireParameters.Add("x-unknown", new StringItem("value"));
        wireParameters.Add("sf", BooleanItem.True);

        var ci = ComponentIdentifier.FromWire("content-type", wireParameters);

        var first = ci.Parameters;
        var second = ci.Parameters;

        ReferenceEquals(first, second).ShouldBeTrue();
        first.Select(kvp => kvp.Key).ShouldBe(["req", "x-unknown", "sf"]);
        first.ShouldNotBeOfType<KeyValuePair<string, BareItem>[]>();
    }

    [Fact]
    public void ToStructuredFieldItem_CalledRepeatedly_DoesNotCorruptCachedParameters()
    {
        // ToStructuredFieldItem() builds a fresh, owned StructuredFieldItem each call (see
        // StructuredFieldItem's copying constructor), so mutating one returned item's
        // Parameters must never leak back into the cached snapshot used by Equals,
        // GetHashCode, and subsequent Serialize/ToStructuredFieldItem calls.
        var ci = new ComponentIdentifier("content-type") { Sf = true };

        var item = ci.ToStructuredFieldItem();
        item.Parameters.Add("extra", BooleanItem.True);

        ci.Parameters.Count.ShouldBe(1);
        ci.Serialize().ShouldBe("\"content-type\";sf");
        ci.ToStructuredFieldItem().Parameters.Count.ShouldBe(1);
    }

    [Fact]
    public void Equals_And_Serialize_ConsistentAfterParametersAccessed()
    {
        // Accessing Parameters (which populates the cache) before Equals/Serialize must not
        // change behavior.
        var a = new ComponentIdentifier("priority") { Key = "u" };
        var b = ComponentIdentifier.FieldKey("priority", "u");

        _ = a.Parameters;

        a.Equals(b).ShouldBeTrue();
        a.GetHashCode().ShouldBe(b.GetHashCode());
        a.Serialize().ShouldBe(b.Serialize());
    }
}
