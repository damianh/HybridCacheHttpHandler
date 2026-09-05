// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using DamianH.Http.StructuredFieldValues;
using Shouldly;

namespace DamianH.Http.HttpSignatures;

public class SignatureParametersTests
{
    [Fact]
    public void Serialize_EmptyCoveredComponents_SerializesEmptyInnerList()
    {
        var sp = new SignatureParameters([]);
        sp.Serialize().ShouldBe("()");
    }

    [Fact]
    public void Serialize_SingleComponent_SerializesCorrectly()
    {
        var sp = new SignatureParameters([ComponentIdentifier.Method]);
        sp.Serialize().ShouldBe("(\"@method\")");
    }

    [Fact]
    public void Serialize_MultipleComponents_SerializesWithSpaces()
    {
        var sp = new SignatureParameters(
        [
            ComponentIdentifier.Method,
            ComponentIdentifier.Authority,
            ComponentIdentifier.Field("content-type"),
        ]);
        sp.Serialize().ShouldBe("(\"@method\" \"@authority\" \"content-type\")");
    }

    [Fact]
    public void Serialize_WithCreated_IncludesCreatedParameter()
    {
        var sp = new SignatureParameters([ComponentIdentifier.Method])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
        };
        sp.Serialize().ShouldBe("(\"@method\");created=1618884473");
    }

    [Fact]
    public void Serialize_WithKeyId_IncludesKeyIdParameter()
    {
        var sp = new SignatureParameters([])
        {
            KeyId = "test-key-rsa-pss",
        };
        sp.Serialize().ShouldBe("();keyid=\"test-key-rsa-pss\"");
    }

    [Fact]
    public void Serialize_WithAlgorithm_IncludesAlgParameter()
    {
        var sp = new SignatureParameters([])
        {
            Algorithm = "rsa-pss-sha512",
        };
        sp.Serialize().ShouldBe("();alg=\"rsa-pss-sha512\"");
    }

    [Fact]
    public void Serialize_WithNonce_IncludesNonceParameter()
    {
        var sp = new SignatureParameters([])
        {
            Nonce = "abc123",
        };
        sp.Serialize().ShouldBe("();nonce=\"abc123\"");
    }

    [Fact]
    public void Serialize_WithExpires_IncludesExpiresParameter()
    {
        var sp = new SignatureParameters([])
        {
            Expires = DateTimeOffset.FromUnixTimeSeconds(1618884773),
        };
        sp.Serialize().ShouldBe("();expires=1618884773");
    }

    [Fact]
    public void Serialize_WithTag_IncludesTagParameter()
    {
        var sp = new SignatureParameters([])
        {
            Tag = "app-tag",
        };
        sp.Serialize().ShouldBe("();tag=\"app-tag\"");
    }

    [Fact]
    public void Serialize_AllParameters_SerializesInCanonicalOrder()
    {
        var sp = new SignatureParameters([ComponentIdentifier.Method])
        {
            Created = DateTimeOffset.FromUnixTimeSeconds(1618884473),
            Expires = DateTimeOffset.FromUnixTimeSeconds(1618884773),
            Nonce = "xyz",
            Algorithm = "hmac-sha256",
            KeyId = "test-shared-secret",
            Tag = "my-app",
        };
        sp.Serialize().ShouldBe(
            "(\"@method\");created=1618884473;expires=1618884773;keyid=\"test-shared-secret\";nonce=\"xyz\";alg=\"hmac-sha256\";tag=\"my-app\"");
    }

    [Fact]
    public void Parse_SimpleInnerList_ReturnsCorrectParameters()
    {
        // Build an InnerList programmatically
        var name = new StringItem("@method");
        var innerList = new InnerList([name]);

        var sp = SignatureParameters.Parse(innerList);

        sp.CoveredComponents.Count.ShouldBe(1);
        sp.CoveredComponents[0].Name.ShouldBe("@method");
    }

    [Fact]
    public void Parse_InnerListWithCreated_ParsesCreatedTimestamp()
    {
        var name = new StringItem("@method");
        var innerList = new InnerList([name]);
        innerList.Parameters.Add("created", new IntegerItem(1618884473));

        var sp = SignatureParameters.Parse(innerList);

        sp.Created.ShouldNotBeNull();
        sp.Created!.Value.ToUnixTimeSeconds().ShouldBe(1618884473);
    }

    [Fact]
    public void Parse_InnerListWithKeyId_ParsesKeyId()
    {
        var innerList = new InnerList([]);
        innerList.Parameters.Add("keyid", new StringItem("my-key"));

        var sp = SignatureParameters.Parse(innerList);

        sp.KeyId.ShouldBe("my-key");
    }

    [Fact]
    public void Parse_ComponentWithSfParam_ParsesSfFlag()
    {
        var name = new StructuredFieldItem(new StringItem("priority"));
        name.Parameters.Add("sf", BooleanItem.True);
        var innerList = new InnerList([name]);

        var sp = SignatureParameters.Parse(innerList);

        sp.CoveredComponents[0].Sf.ShouldBeTrue();
    }

    [Fact]
    public void Parse_ComponentWithReqParam_ParsesReqFlag()
    {
        var name = new StructuredFieldItem(new StringItem("@method"));
        name.Parameters.Add("req", BooleanItem.True);
        var innerList = new InnerList([name]);

        var sp = SignatureParameters.Parse(innerList);

        sp.CoveredComponents[0].Req.ShouldBeTrue();
    }

    [Fact]
    public void Parse_ComponentWithQueryParamName_ParsesQueryParamName()
    {
        var name = new StructuredFieldItem(new StringItem("@query-param"));
        name.Parameters.Add("name", new StringItem("Pet"));
        var innerList = new InnerList([name]);

        var sp = SignatureParameters.Parse(innerList);

        sp.CoveredComponents[0].QueryParamName.ShouldBe("Pet");
    }

    [Fact]
    public void RoundTrip_ParseThenSerialize_ProducesEquivalentOutput()
    {
        // Parse from structured field, then serialize and compare
        var sfInput = "(\"@method\" \"@authority\" \"content-type\");created=1618884473;keyid=\"test-key\"";
        var dict = StructuredFieldParser.ParseDictionary("sig1=" + sfInput);
        var innerList = dict["sig1"].InnerList;

        var sp = SignatureParameters.Parse(innerList);
        var serialized = sp.Serialize();

        serialized.ShouldBe(sfInput);
    }

    [Fact]
    public void Parse_PreservesUnknownParametersAndCanonicalizesValues()
    {
        var dictionary = StructuredFieldParser.ParseDictionary(
            "sig1=(\"@method\";flag=?1);created=1618884473;extra=1.000;enabled=?1;date=@999999999999999;label=%\"caf%c3%a9\"");

        var parameters = SignatureParameters.Parse(dictionary["sig1"].InnerList);

        parameters.Created!.Value.ToUnixTimeSeconds().ShouldBe(1618884473);
        parameters.Serialize().ShouldBe(
            "(\"@method\";flag);created=1618884473;extra=1.0;enabled;date=@999999999999999;label=%\"caf%c3%a9\"");
    }

    [Fact]
    public void Serialize_UsesSharedEscapingForComponentsAndParameters()
    {
        var parameters = new SignatureParameters(
            [ComponentIdentifier.QueryParam("a\"b\\c")])
        {
            KeyId = "key\"\\id",
        };

        parameters.Serialize().ShouldBe(
            "(\"@query-param\";name=\"a\\\"b\\\\c\");keyid=\"key\\\"\\\\id\"");
    }
}
