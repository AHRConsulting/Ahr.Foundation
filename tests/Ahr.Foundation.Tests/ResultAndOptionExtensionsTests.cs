namespace Ahr.Foundation.Tests;

public sealed class ResultAndOptionExtensionsTests
{
    private sealed record Document(string Id, DocumentMetadata Metadata, IReadOnlyList<string> Sections);
    private sealed record DocumentMetadata(string Author, IReadOnlyDictionary<string, string> Attributes);

    [Fact]
    public void ToSuccess_NonNullValue_ReturnsSuccess() => Assert.Equal(42, 42.ToSuccess().Value);

    [Fact]
    public void ToSuccess_NullValue_ThrowsWithValueParameter()
    {
        string value = null!;
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => value.ToSuccess());
        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void ToSuccess_CustomError_NonNullValue_ReturnsSuccess()
    {
        Result<int, string> result = 42.ToSuccess<int, string>();
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void ToSuccess_CustomError_NullValue_ThrowsWithValueParameter()
    {
        string value = null!;
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => value.ToSuccess<string, int>());
        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void ToFailure_ResultFromError_ReturnsFailure()
    {
        Error error = new("failure");
        Result result = error.ToFailure();
        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void ToFailure_ResultOfTFromError_ReturnsFailure()
    {
        Error error = new("failure");
        Result<int> result = error.ToFailure<int>();
        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void ToFailure_Message_ReturnsFailure()
    {
        Result result = "bad".ToFailure();
        Assert.Equal(new Error("bad"), result.Error);
    }

    [Fact]
    public void ToFailure_NullMessage_ThrowsWithMessageParameter()
    {
        string message = null!;
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => message.ToFailure());
        Assert.Equal("message", exception.ParamName);
    }

    [Fact]
    public void ToFailure_ResultOfTFromMessage_ReturnsFailure()
    {
        Result<int> result = "bad".ToFailure<int>();
        Assert.True(result.IsFailure);
        Assert.Equal(new Error("bad"), result.Error);
    }

    [Fact]
    public void ToFailure_ResultOfTFromNullMessage_ThrowsWithMessageParameter()
    {
        string message = null!;
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => message.ToFailure<int>());
        Assert.Equal("message", exception.ParamName);
    }

    [Fact]
    public void ToFailure_NullException_ThrowsWithExceptionParameter()
    {
        Exception exception = null!;
        ArgumentNullException thrown = Assert.Throws<ArgumentNullException>(() => exception.ToFailure());
        Assert.Equal("exception", thrown.ParamName);
    }

    [Fact]
    public void ToFailure_Exception_ReturnsFailure()
    {
        var ex = new InvalidOperationException("boom");
        Result result = ex.ToFailure();
        Assert.True(result.IsFailure);
        Assert.Equal("boom", result.Error.Message);
        Assert.Same(ex, result.Error.Exception);
    }

    [Fact]
    public void ToFailure_ExceptionAndMessage_ReturnsFailure()
    {
        var ex = new InvalidOperationException("inner");
        Result result = ex.ToFailure("custom error message");
        Assert.True(result.IsFailure);
        Assert.Equal("custom error message", result.Error.Message);
        Assert.Same(ex, result.Error.Exception);
    }

    [Fact]
    public void ToFailure_NullExceptionAndMessage_ThrowsWithExceptionParameter()
    {
        Exception ex = null!;
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => ex.ToFailure("msg"));
        Assert.Equal("exception", exception.ParamName);
    }

    [Fact]
    public void ToFailure_ExceptionAndNullMessage_ThrowsWithMessageParameter()
    {
        var ex = new InvalidOperationException("inner");
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => ex.ToFailure(null!));
        Assert.Equal("message", exception.ParamName);
    }

    [Fact]
    public void ToFailure_ResultOfTFromException_ReturnsFailure()
    {
        var ex = new InvalidOperationException("boom");
        Result<int> result = ex.ToFailure<int>();
        Assert.True(result.IsFailure);
        Assert.Equal("boom", result.Error.Message);
        Assert.Same(ex, result.Error.Exception);
    }

    [Fact]
    public void ToFailure_ResultOfTFromNullException_ThrowsWithExceptionParameter()
    {
        Exception ex = null!;
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => ex.ToFailure<int>());
        Assert.Equal("exception", exception.ParamName);
    }

    [Fact]
    public void ToFailure_ResultOfTFromExceptionAndMessage_ReturnsFailure()
    {
        var ex = new InvalidOperationException("inner");
        Result<int> result = ex.ToFailure<int>("custom message");
        Assert.True(result.IsFailure);
        Assert.Equal("custom message", result.Error.Message);
        Assert.Same(ex, result.Error.Exception);
    }

    [Fact]
    public void ToFailure_ResultOfTFromNullExceptionAndMessage_ThrowsWithExceptionParameter()
    {
        Exception ex = null!;
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => ex.ToFailure<int>("msg"));
        Assert.Equal("exception", exception.ParamName);
    }

    [Fact]
    public void ToOption_NonNullValue_ReturnsSome()
    {
        var val = "hello";
        var option = val.ToOption();
        Assert.True(option.IsSome);
        Assert.Equal("hello", option.Value);
    }

    [Fact]
    public void ToOption_NullValue_ReturnsNone()
    {
        string? val = null;
        var option = val.ToOption();
        Assert.True(option.IsNone);
    }

    [Fact]
    public void FirstOrNone_EmptySequence_ReturnsNone()
    {
        int[] empty = [];
        Option<int> option = empty.FirstOrNone();
        Assert.True(option.IsNone);
    }

    [Fact]
    public void FirstOrNone_NonEmptySequence_ReturnsFirstElementAsSome()
    {
        int[] items = [10, 20, 30];
        Option<int> option = items.FirstOrNone();
        Assert.True(option.IsSome);
        Assert.Equal(10, option.Value);
    }

    [Fact]
    public void FirstOrNone_NullSequence_ThrowsWithSourceParameter()
    {
        IEnumerable<int> source = null!;
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => source.FirstOrNone());
        Assert.Equal("source", exception.ParamName);
    }

    [Fact]
    public void FirstOrNone_WithPredicate_MatchingElement_ReturnsSome()
    {
        int[] items = [1, 2, 3, 4, 5];
        Option<int> option = items.FirstOrNone(x => x > 3);
        Assert.True(option.IsSome);
        Assert.Equal(4, option.Value);
    }

    [Fact]
    public void FirstOrNone_WithPredicate_NoMatch_ReturnsNone()
    {
        int[] items = [1, 2, 3];
        Option<int> option = items.FirstOrNone(x => x > 10);
        Assert.True(option.IsNone);
    }

    [Fact]
    public void FirstOrNone_WithPredicate_NullSequence_ThrowsWithSourceParameter()
    {
        IEnumerable<int> source = null!;
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => source.FirstOrNone(x => x > 0));
        Assert.Equal("source", exception.ParamName);
    }

    [Fact]
    public void FirstOrNone_WithPredicate_NullPredicate_ThrowsWithPredicateParameter()
    {
        int[] items = [1, 2, 3];
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => items.FirstOrNone(null!));
        Assert.Equal("predicate", exception.ParamName);
    }

    [Fact]
    public void ToSuccess_ComplexPayload_PreservesExactInstance()
    {
        Document document = CreateDocument();

        Result<Document> result = document.ToSuccess();

        Assert.Same(document, result.Value);
    }

    [Fact]
    public void ToOption_ComplexPayload_PreservesExactInstance()
    {
        Document document = CreateDocument();

        var option = document.ToOption();

        Assert.Same(document, option.Value);
    }

    [Fact]
    public void FirstOrNone_ComplexPayload_PreservesSequenceInstance()
    {
        Document first = CreateDocument();
        Document second = first with
        {
            Id = "doc-2"
        };

        Option<Document> option = new[] { first, second }.FirstOrNone(document => document.Sections.Count > 1);

        Assert.Same(first, option.Value);
    }

    private static Document CreateDocument() =>
        new(
            "doc-1",
            new DocumentMetadata("Ada", new Dictionary<string, string> { ["format"] = "markdown" }),
            ["Introduction", "Details"]);
}
