namespace Ahr.Foundation.Tests;

public sealed class FormattingTests
{
    private static readonly Error _failure = Error.FromMessage("not found");

    public static TheoryData<object, string> FormattedStates => new()
    {
        { Result.Success(), "Success" },
        { Result.Failure(_failure), "Failure(not found)" },
        { default(Result), "Uninitialized" },
        { Result<int>.Success(42), "Success(42)" },
        { Result<int>.Failure(_failure), "Failure(not found)" },
        { default(Result<int>), "Uninitialized" },
        { Result<int, int>.Success(42), "Success(42)" },
        { Result<int, int>.Failure(404), "Failure(404)" },
        { default(Result<int, int>), "Uninitialized" },
        { Option<int>.Some(42), "Some(42)" },
        { Option<int>.None, "None" },
    };

    [Theory]
    [MemberData(nameof(FormattedStates))]
    public void ToString_EveryState_ReturnsReadableText(object value, string expected) => Assert.Equal(expected, value.ToString());

    [Fact]
    public void Interpolation_MixedStates_UsesToString()
    {
        var text = $"{Result.Success()} {Result<int>.Failure(_failure)} {Option<string>.None}";
        Assert.Equal("Success Failure(not found) None", text);
    }

    [Fact]
    public void Error_Default_Message_IsEmpty()
    {
        Error error = default;
        Assert.Equal(string.Empty, error.Message);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Error_Default_IsStillRejectedByFactories()
    {
        _ = Assert.Throws<ArgumentNullException>(() => Result.Failure(default));
        _ = Assert.Throws<ArgumentNullException>(() => Result<int>.Failure(default));
        _ = Assert.Throws<ArgumentNullException>(() => Option<int>.None.ToResult(new Error()));
    }

    [Fact]
    public void FirstOrNone_NullFirstElement_ReturnsNone() => Assert.True(new string?[] { null, "a" }.FirstOrNone().IsNone);

    [Fact]
    public void FirstOrNone_PredicateMatchesNull_ReturnsNone() => Assert.True(new string?[] { "a", null }.FirstOrNone(x => x is null).IsNone);

    [Fact]
    public void ToOption_NullableValueType_ReturnsOptionOfUnderlyingType()
    {
        int? present = 5;
        int? missing = null;
        var some = present.ToOption();
        var none = missing.ToOption();
        _ = Assert.IsType<Option<int>>(some);
        Assert.Equal(5, some.Value);
        Assert.True(none.IsNone);
    }
}
