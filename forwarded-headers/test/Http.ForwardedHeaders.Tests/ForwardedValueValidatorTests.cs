// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using Shouldly;

namespace DamianH.Http.ForwardedHeaders;

public sealed class ForwardedValueValidatorTests
{
    [Fact]
    public void EmptyPathBaseUsesExplicitEmptyPathString()
    {
        ForwardedValueValidator.TryPathBase("", out var pathBase).ShouldBeTrue();
        pathBase.Value.ShouldBe("");
    }
}
