// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Tests for <see cref="ComponentValidator"/> per RFC 9421 §2.5, exercising the strict component
/// parameter validation shared by <see cref="FieldComponentResolver"/> and
/// <see cref="DerivedComponentResolver"/>.
/// </summary>
public sealed class ComponentValidatorTests
{
    private static TestHttpMessageContext RequestContext() =>
        TestHttpMessageContext.CreateRequest("GET", "https", "example.com", "/");

    private static TestHttpMessageContext ResponseContext() =>
        TestHttpMessageContext.CreateResponse(200, RequestContext());

    [Fact]
    public void Validate_SignatureParamsComponent_Throws()
    {
        var id = new ComponentIdentifier("@signature-params");
        Should.Throw<SignatureBaseException>(() => ComponentValidator.Validate(id, RequestContext()));
    }

    [Fact]
    public void Validate_UnknownParameter_Throws()
    {
        var wireParameters = new DamianH.Http.StructuredFieldValues.Parameters();
        wireParameters.Add("unsupported", DamianH.Http.StructuredFieldValues.BooleanItem.True);
        var id = ComponentIdentifier.FromWire("content-type", wireParameters);

        Should.Throw<SignatureBaseException>(() => ComponentValidator.Validate(id, RequestContext()));
    }

    [Fact]
    public void Validate_BsCombinedWithSf_Throws()
    {
        var id = new ComponentIdentifier("content-type") { Bs = true, Sf = true };
        Should.Throw<SignatureBaseException>(() => ComponentValidator.Validate(id, RequestContext()));
    }

    [Fact]
    public void Validate_BsCombinedWithKey_Throws()
    {
        var id = new ComponentIdentifier("content-type") { Bs = true, Key = "a" };
        Should.Throw<SignatureBaseException>(() => ComponentValidator.Validate(id, RequestContext()));
    }

    [Fact]
    public void Validate_SfAndKeyTogether_DoesNotThrow()
    {
        var id = new ComponentIdentifier("content-type") { Sf = true, Key = "a" };
        Should.NotThrow(() => ComponentValidator.Validate(id, RequestContext()));
    }

    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    public void Validate_DerivedComponentWithFieldOnlyParameter_Throws(bool sf, bool bsUnused, bool bs, bool tr)
    {
        var id = new ComponentIdentifier("@method") { Sf = sf, Bs = bs, Tr = tr };
        Should.Throw<SignatureBaseException>(() => ComponentValidator.Validate(id, RequestContext()));
    }

    [Fact]
    public void Validate_DerivedComponentWithKeyParameter_Throws()
    {
        var id = new ComponentIdentifier("@method") { Key = "a" };
        Should.Throw<SignatureBaseException>(() => ComponentValidator.Validate(id, RequestContext()));
    }

    [Fact]
    public void Validate_QueryParamWithoutName_Throws()
    {
        var id = new ComponentIdentifier("@query-param");
        Should.Throw<SignatureBaseException>(() => ComponentValidator.Validate(id, RequestContext()));
    }

    [Fact]
    public void Validate_NameParameterOnNonQueryParamDerivedComponent_Throws()
    {
        var wireParameters = new DamianH.Http.StructuredFieldValues.Parameters();
        wireParameters.Add("name", new DamianH.Http.StructuredFieldValues.StringItem("x"));
        var id = ComponentIdentifier.FromWire("@method", wireParameters);

        Should.Throw<SignatureBaseException>(() => ComponentValidator.Validate(id, RequestContext()));
    }

    [Fact]
    public void Validate_NameParameterOnField_Throws()
    {
        var wireParameters = new DamianH.Http.StructuredFieldValues.Parameters();
        wireParameters.Add("name", new DamianH.Http.StructuredFieldValues.StringItem("x"));
        var id = ComponentIdentifier.FromWire("content-type", wireParameters);

        Should.Throw<SignatureBaseException>(() => ComponentValidator.Validate(id, RequestContext()));
    }

    [Fact]
    public void Validate_ReqOnRequestMessage_Throws()
    {
        var id = new ComponentIdentifier("content-type") { Req = true };
        Should.Throw<SignatureBaseException>(() => ComponentValidator.Validate(id, RequestContext()));
    }

    [Fact]
    public void Validate_ReqOnResponseMessage_DoesNotThrow()
    {
        var id = new ComponentIdentifier("content-type") { Req = true };
        Should.NotThrow(() => ComponentValidator.Validate(id, ResponseContext()));
    }

    [Fact]
    public void Validate_PlainField_DoesNotThrow()
    {
        var id = ComponentIdentifier.Field("content-type");
        Should.NotThrow(() => ComponentValidator.Validate(id, RequestContext()));
    }
}
