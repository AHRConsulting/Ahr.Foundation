namespace Ahr.Foundation.Tests;

public sealed class LinqExtensionsTests
{
    private static Result<int> Succeed(int value) => Result<int>.Success(value);
    private static Result<int> Fail(string message) => Result<int>.Failure(Error.FromMessage(message));

    [Fact]
    public void Select_Result_Success_ProjectsValue()
    {
        Result<int> result = from value in Succeed(21) select value * 2;
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Select_Result_Failure_PreservesError()
    {
        Result<int> result = from value in Fail("boom") select value * 2;
        Assert.True(result.IsFailure);
        Assert.Equal("boom", result.Error.Message);
    }

    [Fact]
    public void Select_Result_Failure_DoesNotInvokeProjection()
    {
        var invoked = false;
        _ = from value in Fail("boom")
            select Track(() => invoked = true, value);
        Assert.False(invoked);
    }

    [Fact]
    public void Select_CustomError_Success_ProjectsValue()
    {
        Result<string, int> result = from value in Result<int, int>.Success(7) select value.ToString();
        Assert.True(result.IsSuccess);
        Assert.Equal("7", result.Value);
    }

    [Fact]
    public void Select_CustomError_Failure_PreservesError()
    {
        Result<string, int> result = from value in Result<int, int>.Failure(404) select value.ToString();
        Assert.True(result.IsFailure);
        Assert.Equal(404, result.Error);
    }

    [Fact]
    public void Select_Option_Some_ProjectsValue()
    {
        Option<int> option = from value in Option<int>.Some(21) select value * 2;
        Assert.True(option.IsSome);
        Assert.Equal(42, option.Value);
    }

    [Fact]
    public void Select_Option_None_StaysNone()
    {
        Option<int> option = from value in Option<int>.None select value * 2;
        Assert.True(option.IsNone);
    }

    [Fact]
    public void Select_MatchesMapBehaviour()
    {
        Result<int> viaQuery = from value in Succeed(10) select value + 1;
        Result<int> viaMap = Succeed(10).Map(value => value + 1);
        Assert.Equal(viaMap, viaQuery);
    }

    [Fact]
    public void SelectMany_Result_AllSucceed_ProjectsBothValues()
    {
        Result<int> result =
            from first in Succeed(20)
            from second in Succeed(22)
            select first + second;

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void SelectMany_Result_FirstFails_ShortCircuits()
    {
        var secondEvaluated = false;
        Result<int> result =
            from first in Fail("first failed")
            from second in Track(() => secondEvaluated = true, Succeed(22))
            select first + second;

        Assert.True(result.IsFailure);
        Assert.Equal("first failed", result.Error.Message);
        Assert.False(secondEvaluated);
    }

    [Fact]
    public void SelectMany_Result_SecondFails_PropagatesSecondError()
    {
        Result<int> result =
            from first in Succeed(20)
            from second in Fail("second failed")
            select first + second;

        Assert.True(result.IsFailure);
        Assert.Equal("second failed", result.Error.Message);
    }

    [Fact]
    public void SelectMany_Result_LaterClauseSeesEarlierValue()
    {
        Result<int> result =
            from first in Succeed(6)
            from second in Succeed(first * 7)
            select second;

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void SelectMany_Result_ThreeClauses_Compose()
    {
        Result<int> result =
            from first in Succeed(1)
            from second in Succeed(2)
            from third in Succeed(3)
            select first + second + third;

        Assert.True(result.IsSuccess);
        Assert.Equal(6, result.Value);
    }

    [Fact]
    public void SelectMany_CustomError_ShortCircuitsOnFirstFailure()
    {
        Result<int, string> result =
            from first in Result<int, string>.Failure("invalid")
            from second in Result<int, string>.Success(2)
            select first + second;

        Assert.True(result.IsFailure);
        Assert.Equal("invalid", result.Error);
    }

    [Fact]
    public void SelectMany_CustomError_AllSucceed_ProjectsBothValues()
    {
        Result<int, string> result =
            from first in Result<int, string>.Success(20)
            from second in Result<int, string>.Success(22)
            select first + second;

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void SelectMany_Option_BothSome_ProjectsBothValues()
    {
        Option<int> option =
            from first in Option<int>.Some(20)
            from second in Option<int>.Some(22)
            select first + second;

        Assert.True(option.IsSome);
        Assert.Equal(42, option.Value);
    }

    [Fact]
    public void SelectMany_Option_FirstNone_ShortCircuits()
    {
        var secondEvaluated = false;
        Option<int> option =
            from first in Option<int>.None
            from second in Track(() => secondEvaluated = true, Option<int>.Some(22))
            select first + second;

        Assert.True(option.IsNone);
        Assert.False(secondEvaluated);
    }

    [Fact]
    public void SelectMany_Option_SecondNone_ReturnsNone()
    {
        Option<int> option =
            from first in Option<int>.Some(20)
            from second in Option<int>.None
            select first + second;

        Assert.True(option.IsNone);
    }

    [Fact]
    public void SelectMany_MatchesBindBehaviour()
    {
        Result<int> viaQuery =
            from first in Succeed(20)
            from second in Succeed(22)
            select first + second;
        Result<int> viaBind = Succeed(20).Bind(first => Succeed(22).Map(second => first + second));
        Assert.Equal(viaBind, viaQuery);
    }

    [Fact]
    public void SelectMany_Result_NullSelector_ThrowsWithSelectorParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => Succeed(1).SelectMany(null!, (int first, int second) => first + second));
        Assert.Equal("selector", exception.ParamName);
    }

    [Fact]
    public void SelectMany_Result_NullResultSelector_ThrowsWithResultSelectorParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => Succeed(1).SelectMany<int, int, int>(Succeed, null!));
        Assert.Equal("resultSelector", exception.ParamName);
    }

    [Fact]
    public void SelectMany_CustomError_NullSelector_ThrowsWithSelectorParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => Result<int, string>.Success(1)
                .SelectMany(null!, (int first, int second) => first + second));
        Assert.Equal("selector", exception.ParamName);
    }

    [Fact]
    public void SelectMany_CustomError_NullResultSelector_ThrowsWithResultSelectorParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => Result<int, string>.Success(1)
                .SelectMany<int, string, int, int>(Result<int, string>.Success, null!));
        Assert.Equal("resultSelector", exception.ParamName);
    }

    [Fact]
    public void SelectMany_Option_NullSelector_ThrowsWithSelectorParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => Option<int>.Some(1).SelectMany(null!, (int first, int second) => first + second));
        Assert.Equal("selector", exception.ParamName);
    }

    [Fact]
    public void SelectMany_Option_NullResultSelector_ThrowsWithResultSelectorParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => Option<int>.Some(1).SelectMany<int, int, int>(Option<int>.Some, null!));
        Assert.Equal("resultSelector", exception.ParamName);
    }

    private static T Track<T>(Action onEvaluated, T value)
    {
        onEvaluated();
        return value;
    }
}
