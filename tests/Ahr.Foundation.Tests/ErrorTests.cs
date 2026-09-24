namespace Ahr.Foundation.Tests;

public sealed class ErrorTests
{
    [Fact]
    public void Constructor_MessageAndException_PreservesBoth()
    {
        var exception = new InvalidOperationException("inner");
        var error = new Error("outer", exception);
        Assert.Equal("outer", error.Message);
        Assert.Same(exception, error.Exception);
    }

    [Fact]
    public void Constructor_NullMessage_ThrowsWithMessageParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new Error(null!));
        Assert.Equal("message", exception.ParamName);
    }

    [Fact]
    public void FromException_NullException_ThrowsWithExceptionParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Error.FromException(null!));
        Assert.Equal("exception", exception.ParamName);
    }

    [Fact]
    public void FromException_ValidException_PreservesIdentityAndMessage()
    {
        var source = new InvalidOperationException("broken");
        var error = Error.FromException(source);
        Assert.Equal("broken", error.Message);
        Assert.Same(source, error.Exception);
    }

    [Fact]
    public void Equality_EquivalentErrors_AreEqualAndHashCodesMatch()
    {
        var left = new Error("same");
        var right = new Error("same");
        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void WithExpression_NullMessage_ThrowsWithValueParameter()
    {
        Error error = new("valid");
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => error with { Message = null! });
        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void FromMessage_ReturnsErrorWithMessage()
    {
        var error = Error.FromMessage("custom error");
        Assert.Equal("custom error", error.Message);
        Assert.Null(error.Exception);
    }

    [Fact]
    public void FromMessage_NullMessage_ThrowsWithMessageParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Error.FromMessage(null!));
        Assert.Equal("message", exception.ParamName);
    }

    [Fact]
    public void ToString_ReturnsMessage()
    {
        Error error = new("failed to connect");
        Assert.Equal("failed to connect", error.ToString());
    }
}
