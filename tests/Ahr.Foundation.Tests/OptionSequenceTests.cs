namespace Ahr.Foundation.Tests;

public sealed class OptionSequenceTests
{
    // -- SingleOrNone --

    [Fact]
    public void SingleOrNone_Empty_ReturnsNone() => Assert.True(Array.Empty<int>().SingleOrNone().IsNone);

    [Fact]
    public void SingleOrNone_OneElement_ReturnsSome() => Assert.Equal(7, new[] { 7 }.SingleOrNone().Value);

    [Fact]
    public void SingleOrNone_NullElement_ReturnsNone() => Assert.True(new string?[] { null }.SingleOrNone().IsNone);

    [Fact]
    public void SingleOrNone_MoreThanOneElement_Throws() =>
        _ = Assert.Throws<InvalidOperationException>(() => new[] { 1, 2 }.SingleOrNone());

    [Fact]
    public void SingleOrNone_Predicate_OneMatch_ReturnsSome() => Assert.Equal(2, new[] { 1, 2, 3 }.SingleOrNone(x => x == 2).Value);

    [Fact]
    public void SingleOrNone_Predicate_NoMatch_ReturnsNone() => Assert.True(new[] { 1, 2 }.SingleOrNone(x => x > 5).IsNone);

    [Fact]
    public void SingleOrNone_Predicate_ManyMatches_Throws() =>
        _ = Assert.Throws<InvalidOperationException>(() => new[] { 1, 2, 3 }.SingleOrNone(x => x > 1));

    [Fact]
    public void SingleOrNone_NullArguments_Throw()
    {
        ArgumentNullException source = Assert.Throws<ArgumentNullException>(() => ((IEnumerable<int>)null!).SingleOrNone());
        ArgumentNullException predicate = Assert.Throws<ArgumentNullException>(() => new[] { 1 }.SingleOrNone(null!));
        Assert.Equal("source", source.ParamName);
        Assert.Equal("predicate", predicate.ParamName);
    }

    // -- LastOrNone --

    [Fact]
    public void LastOrNone_Empty_ReturnsNone() => Assert.True(new List<int>().LastOrNone().IsNone);

    [Fact]
    public void LastOrNone_List_ReturnsLast() => Assert.Equal(3, new List<int> { 1, 2, 3 }.LastOrNone().Value);

    [Fact]
    public void LastOrNone_LazySequence_ReturnsLast() => Assert.Equal(3, Enumerable.Range(1, 3).Select(x => x).LastOrNone().Value);

    [Fact]
    public void LastOrNone_EmptyLazySequence_ReturnsNone() => Assert.True(Enumerable.Empty<int>().Select(x => x).LastOrNone().IsNone);

    [Fact]
    public void LastOrNone_NullLastElement_ReturnsNone() => Assert.True(new string?[] { "a", null }.LastOrNone().IsNone);

    [Fact]
    public void LastOrNone_Predicate_List_ReturnsLastMatch() => Assert.Equal(4, new[] { 1, 2, 3, 4, 5 }.LastOrNone(x => x % 2 == 0).Value);

    [Fact]
    public void LastOrNone_Predicate_LazySequence_ReturnsLastMatch() =>
        Assert.Equal(4, Enumerable.Range(1, 5).Select(x => x).LastOrNone(x => x % 2 == 0).Value);

    [Fact]
    public void LastOrNone_Predicate_NoMatch_ReturnsNone() => Assert.True(new[] { 1, 3 }.LastOrNone(x => x % 2 == 0).IsNone);

    [Fact]
    public void LastOrNone_NullArguments_Throw()
    {
        ArgumentNullException source = Assert.Throws<ArgumentNullException>(() => ((IEnumerable<int>)null!).LastOrNone());
        ArgumentNullException predicate = Assert.Throws<ArgumentNullException>(() => new[] { 1 }.LastOrNone(null!));
        Assert.Equal("source", source.ParamName);
        Assert.Equal("predicate", predicate.ParamName);
    }

    // -- GetValueOrNone --

    [Fact]
    public void GetValueOrNone_KeyPresent_ReturnsSome()
    {
        var prices = new Dictionary<string, decimal> { ["apple"] = 1.5m };
        Assert.Equal(1.5m, prices.GetValueOrNone("apple").Value);
    }

    [Fact]
    public void GetValueOrNone_KeyMissing_ReturnsNone()
    {
        var prices = new Dictionary<string, decimal>();
        Assert.True(prices.GetValueOrNone("pear").IsNone);
    }

    [Fact]
    public void GetValueOrNone_NullValue_ReturnsNone()
    {
        var names = new Dictionary<int, string?> { [1] = null };
        Assert.True(names.GetValueOrNone(1).IsNone);
    }

    [Fact]
    public void GetValueOrNone_ReadOnlyDictionary_ReturnsSome()
    {
        IReadOnlyDictionary<int, string> names = new System.Collections.ObjectModel.ReadOnlyDictionary<int, string>(new Dictionary<int, string> { [1] = "one" });
        Assert.Equal("one", names.GetValueOrNone(1).Value);
    }

    [Fact]
    public void GetValueOrNone_NullDictionary_Throws()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => ((IReadOnlyDictionary<int, int>)null!).GetValueOrNone(1));
        Assert.Equal("dictionary", exception.ParamName);
    }

    // -- Values --

    [Fact]
    public void Values_MixedOptions_ReturnsSomeValuesInOrder()
    {
        Option<int>[] options = [Option<int>.Some(1), Option<int>.None, Option<int>.Some(3)];
        Assert.Equal([1, 3], options.Values());
    }

    [Fact]
    public void Values_IsDeferred()
    {
        var evaluated = 0;
        IEnumerable<int> values = Enumerable.Range(1, 3).Select(x => { evaluated++; return Option<int>.Some(x); }).Values();

        Assert.Equal(0, evaluated);
        Assert.Equal([1, 2, 3], [.. values]);
        Assert.Equal(3, evaluated);
    }

    [Fact]
    public void Values_NullSource_ThrowsImmediately()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => ((IEnumerable<Option<int>>)null!).Values());
        Assert.Equal("source", exception.ParamName);
    }
}
