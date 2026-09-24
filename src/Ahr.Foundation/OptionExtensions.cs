namespace Ahr.Foundation;

/// <summary>Factory and sequence helpers for options.</summary>
public static class OptionExtensions
{
    /// <summary>Returns the first element as Some, or None.</summary>
    public static Option<T> FirstOrNone<T>(this IEnumerable<T> source)
    {
        _ = Guard.NotNull(source, nameof(source));
        using IEnumerator<T> enumerator = source.GetEnumerator();
        return enumerator
            .MoveNext()
            ? Option<T>.Some(enumerator.Current)
            : Option<T>.None;
    }
    /// <summary>Returns the first matching element as Some, or None.</summary>
    public static Option<T> FirstOrNone<T>(this IEnumerable<T> source, Func<T, bool> predicate)
    {
        _ = Guard.NotNull(source, nameof(source));
        _ = Guard.NotNull(predicate, nameof(predicate));
        return source
        .Where(predicate)
        .FirstOrNone();

    }
    /// <summary>Creates None for null or Some for a non-null value.</summary>
    public static Option<T> ToOption<T>(this T? value) => value is null ? Option<T>.None : Option<T>.Some(value);
}
