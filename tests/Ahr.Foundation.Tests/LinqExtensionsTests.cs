namespace Ahr.Foundation.Tests;

public sealed class LinqExtensionsTests
{
    private static readonly BusinessContext _linqComposition = BusinessContext
        .Define("functional-query-composition", "Functional query composition", "Compose Result and Option values using LINQ query syntax.")
        .Capability("project-functional-values", "Project Result and Option values")
        .Capability("compose-dependent-results", "Compose dependent Result and Option operations")
        .Outcome("query-failure-short-circuit", "A failed Result or empty Option stops later query clauses")
        .Outcome("query-success-projection", "Successful query clauses project the expected value")
        .Rule("query-propagates-failures", "Query syntax preserves failures and empty options")
        .Build();

    private static Result<int> Succeed(int value) => Result<int>.Success(value);
    private static Result<int> Fail(string message) => Result<int>.Failure(Error.FromMessage(message));

    [Fact]
    public Task Select_Result_Success_ProjectsValue()
    {
        Result<int>? result = null;

        return Scenario.Define("Selecting from a successful result projects its value", _linqComposition)
            .When("The value is selected and doubled", () =>
            {
                result = from value in Succeed(21) select value * 2;
            })
            .Then("The projected result contains the doubled value", () =>
            {
                Assert.True(result?.IsSuccess);
                Assert.Equal(42, result?.Value);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task Select_Result_Failure_PreservesError()
    {
        Result<int>? result = null;

        return Scenario.Define("Selecting from a failed result preserves its error", _linqComposition)
            .When("A projection is applied to a failed result", () =>
            {
                result = from value in Fail("boom") select value * 2;
            })
            .Then("The original failure is returned", () =>
            {
                Assert.True(result?.IsFailure);
                Assert.Equal("boom", result?.Error.Message);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task Select_Result_Failure_DoesNotInvokeProjection()
    {
        var invoked = false;
        Result<int>? result = null;

        return Scenario.Define("A failed result skips its query projection", _linqComposition)
            .When("The failed result is selected through a tracked projection", () =>
            {
                result =
                    from value in Fail("boom")
                    select Track(() => invoked = true, value);
            })
            .Then("The failure is preserved", () =>
                Assert.True(result?.IsFailure))
            .And("The projection is not invoked", () =>
                Assert.False(invoked))
            .VerifyAsync();
    }

    [Fact]
    public Task Select_CustomError_Success_ProjectsValue()
    {
        Result<string, int>? result = null;

        return Scenario.Define("Selecting from a custom-error result projects its value", _linqComposition)
            .When("The successful custom-error value is converted to text", () =>
            {
                result = from value in Result<int, int>.Success(7) select value.ToString();
            })
            .Then("The projected result contains the text", () =>
            {
                Assert.True(result?.IsSuccess);
                Assert.Equal("7", result?.Value);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task Select_CustomError_Failure_PreservesError()
    {
        Result<string, int>? result = null;

        return Scenario.Define("Selecting from a failed custom-error result preserves its error", _linqComposition)
            .When("A projection is applied to a custom-error failure", () =>
            {
                result = from value in Result<int, int>.Failure(404) select value.ToString();
            })
            .Then("The original custom error is returned", () =>
            {
                Assert.True(result?.IsFailure);
                Assert.Equal(404, result?.Error);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task Select_Option_Some_ProjectsValue()
    {
        Option<int>? option = null;

        return Scenario.Define("Selecting from an option with a value projects it", _linqComposition)
            .When("The option value is selected and doubled", () =>
            {
                option = from value in Option<int>.Some(21) select value * 2;
            })
            .Then("The projected option contains the doubled value", () =>
            {
                Assert.True(option?.IsSome);
                Assert.Equal(42, option?.Value);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task Select_Option_None_StaysNone()
    {
        Option<int>? option = null;

        return Scenario.Define("Selecting from an empty option remains empty", _linqComposition)
            .When("A projection is applied to an empty option", () =>
            {
                option = from value in Option<int>.None select value * 2;
            })
            .Then("The projected option remains empty", () =>
                Assert.True(option?.IsNone))
            .VerifyAsync();
    }

    [Fact]
    public Task Select_MatchesMapBehaviour()
    {
        Result<int>? viaQuery = null;
        Result<int>? viaMap = null;

        return Scenario.Define("Selecting from a result matches mapping it", _linqComposition)
            .When("The result is projected with query syntax and Map", () =>
            {
                viaQuery = from value in Succeed(10) select value + 1;
                viaMap = Succeed(10).Map(value => value + 1);
            })
            .Then("Both projections return the same result", () =>
                Assert.Equal(viaMap!.Value, viaQuery!.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task SelectMany_Result_AllSucceed_ProjectsBothValues()
    {
        Result<int>? result = null;

        return Scenario.Define("A successful result query combines both values", _linqComposition)
            .When("Two successful results are combined", () =>
            {
                result =
                    from first in Succeed(20)
                    from second in Succeed(22)
                    select first + second;
            })
            .Then("The query returns the sum", () =>
            {
                Assert.True(result?.IsSuccess);
                Assert.Equal(42, result?.Value);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task SelectMany_Result_FirstFails_ShortCircuits()
    {
        var secondEvaluated = false;
        Result<int>? result = null;

        return Scenario.Define("A failed first result stops later query clauses", _linqComposition)
            .When("The query starts with a failed result", () =>
            {
                result =
                    from first in Fail("first failed")
                    from second in Track(() => secondEvaluated = true, Succeed(22))
                    select first + second;
            })
            .Then("The first failure is returned", () =>
            {
                Assert.True(result?.IsFailure);
                Assert.Equal("first failed", result?.Error.Message);
            })
            .And("The later clause is not evaluated", () =>
                Assert.False(secondEvaluated))
            .VerifyAsync();
    }

    [Fact]
    public Task SelectMany_Result_SecondFails_PropagatesSecondError()
    {
        Result<int>? result = null;

        return Scenario.Define("A failed later result returns its error", _linqComposition)
            .When("The query succeeds first and then encounters a failure", () =>
            {
                result =
                    from first in Succeed(20)
                    from second in Fail("second failed")
                    select first + second;
            })
            .Then("The later failure is returned", () =>
            {
                Assert.True(result?.IsFailure);
                Assert.Equal("second failed", result?.Error.Message);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task SelectMany_Result_LaterClauseSeesEarlierValue()
    {
        Result<int>? result = null;

        return Scenario.Define("A later result clause can use an earlier value", _linqComposition)
            .When("The later clause derives its value from the first result", () =>
            {
                result =
                    from first in Succeed(6)
                    from second in Succeed(first * 7)
                    select second;
            })
            .Then("The query returns the derived value", () =>
            {
                Assert.True(result?.IsSuccess);
                Assert.Equal(42, result?.Value);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task SelectMany_Result_ThreeClauses_Compose()
    {
        Result<int>? result = null;

        return Scenario.Define("Three successful result clauses compose into one value", _linqComposition)
            .When("Three results are combined with query syntax", () =>
            {
                result =
                    from first in Succeed(1)
                    from second in Succeed(2)
                    from third in Succeed(3)
                    select first + second + third;
            })
            .Then("The query returns the sum of all clauses", () =>
            {
                Assert.True(result?.IsSuccess);
                Assert.Equal(6, result?.Value);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task SelectMany_CustomError_ShortCircuitsOnFirstFailure()
    {
        Result<int, string>? result = null;

        return Scenario.Define("A failed custom-error result stops the query", _linqComposition)
            .When("The query starts with a custom-error failure", () =>
            {
                result =
                    from first in Result<int, string>.Failure("invalid")
                    from second in Result<int, string>.Success(2)
                    select first + second;
            })
            .Then("The original custom error is returned", () =>
            {
                Assert.True(result?.IsFailure);
                Assert.Equal("invalid", result?.Error);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task SelectMany_CustomError_AllSucceed_ProjectsBothValues()
    {
        Result<int, string>? result = null;

        return Scenario.Define("Successful custom-error results combine their values", _linqComposition)
            .When("Two successful custom-error results are combined", () =>
            {
                result =
                    from first in Result<int, string>.Success(20)
                    from second in Result<int, string>.Success(22)
                    select first + second;
            })
            .Then("The query returns the sum", () =>
            {
                Assert.True(result?.IsSuccess);
                Assert.Equal(42, result?.Value);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task SelectMany_Option_BothSome_ProjectsBothValues()
    {
        Option<int>? option = null;

        return Scenario.Define("Two options with values combine through query syntax", _linqComposition)
            .When("Both option values are combined", () =>
            {
                option =
                    from first in Option<int>.Some(20)
                    from second in Option<int>.Some(22)
                    select first + second;
            })
            .Then("The query returns the sum", () =>
            {
                Assert.True(option?.IsSome);
                Assert.Equal(42, option?.Value);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task SelectMany_Option_FirstNone_ShortCircuits()
    {
        var secondEvaluated = false;
        Option<int>? option = null;

        return Scenario.Define("An empty first option stops later query clauses", _linqComposition)
            .When("The query starts with an empty option", () =>
            {
                option =
                    from first in Option<int>.None
                    from second in Track(() => secondEvaluated = true, Option<int>.Some(22))
                    select first + second;
            })
            .Then("The query remains empty", () =>
                Assert.True(option?.IsNone))
            .And("The later clause is not evaluated", () =>
                Assert.False(secondEvaluated))
            .VerifyAsync();
    }

    [Fact]
    public Task SelectMany_Option_SecondNone_ReturnsNone()
    {
        Option<int>? option = null;

        return Scenario.Define("An empty later option makes the query empty", _linqComposition)
            .When("The query encounters an empty option after a value", () =>
            {
                option =
                    from first in Option<int>.Some(20)
                    from second in Option<int>.None
                    select first + second;
            })
            .Then("The query result is empty", () =>
                Assert.True(option?.IsNone))
            .VerifyAsync();
    }

    [Fact]
    public Task SelectMany_MatchesBindBehaviour()
    {
        Result<int>? viaQuery = null;
        Result<int>? viaBind = null;

        return Scenario.Define("A multi-clause result query matches Bind composition", _linqComposition)
            .When("The same result operation is composed with query syntax and Bind", () =>
            {
                viaQuery =
                    from first in Succeed(20)
                    from second in Succeed(22)
                    select first + second;
                viaBind = Succeed(20).Bind(first => Succeed(22).Map(second => first + second));
            })
            .Then("Both compositions return the same result", () =>
                Assert.Equal(viaBind!.Value, viaQuery!.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task SelectMany_Result_NullSelector_ThrowsWithSelectorParameter() =>
        Scenario.Define("A result query requires a selector", _linqComposition)
            .WhenThrows<ArgumentNullException>("A null result selector is supplied", () =>
            _ = Succeed(1).SelectMany(null!, (int first, int second) => first + second))
            .Then("The missing selector is identified", (ArgumentNullException refusal) =>
                Assert.Equal("selector", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task SelectMany_Result_NullResultSelector_ThrowsWithResultSelectorParameter() =>
        Scenario.Define("A result query requires a result selector", _linqComposition)
            .WhenThrows<ArgumentNullException>("A null result selector is supplied", () =>
            _ = Succeed(1).SelectMany<int, int, int>(Succeed, null!))
            .Then("The missing result selector is identified", (ArgumentNullException refusal) =>
                Assert.Equal("resultSelector", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task SelectMany_CustomError_NullSelector_ThrowsWithSelectorParameter() =>
        Scenario.Define("A custom-error result query requires a selector", _linqComposition)
            .WhenThrows<ArgumentNullException>("A null custom-error selector is supplied", () =>
            _ = Result<int, string>.Success(1)
                    .SelectMany(null!, (int first, int second) => first + second))
            .Then("The missing selector is identified", (ArgumentNullException refusal) =>
                Assert.Equal("selector", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task SelectMany_CustomError_NullResultSelector_ThrowsWithResultSelectorParameter() =>
        Scenario.Define("A custom-error result query requires a result selector", _linqComposition)
            .WhenThrows<ArgumentNullException>("A null custom-error result selector is supplied", () =>
            _ = Result<int, string>.Success(1)
                    .SelectMany<int, string, int, int>(Result<int, string>.Success, null!))
            .Then("The missing result selector is identified", (ArgumentNullException refusal) =>
                Assert.Equal("resultSelector", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task SelectMany_Option_NullSelector_ThrowsWithSelectorParameter() =>
        Scenario.Define("An option query requires a selector", _linqComposition)
            .WhenThrows<ArgumentNullException>("A null option selector is supplied", () =>
            _ = Option<int>.Some(1).SelectMany(null!, (int first, int second) => first + second))
            .Then("The missing selector is identified", (ArgumentNullException refusal) =>
                Assert.Equal("selector", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task SelectMany_Option_NullResultSelector_ThrowsWithResultSelectorParameter() =>
        Scenario.Define("An option query requires a result selector", _linqComposition)
            .WhenThrows<ArgumentNullException>("A null option result selector is supplied", () =>
            _ = Option<int>.Some(1).SelectMany<int, int, int>(Option<int>.Some, null!))
            .Then("The missing result selector is identified", (ArgumentNullException refusal) =>
                Assert.Equal("resultSelector", refusal.ParamName))
            .VerifyAsync();

    private static T Track<T>(Action onEvaluated, T value)
    {
        onEvaluated();
        return value;
    }
}
