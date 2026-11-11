namespace Ahr.Foundation;

/// <summary>Factory and sequence helpers for options.</summary>
public static class OptionExtensions
{
    /// <summary>Returns the first element as Some, or None when the sequence is empty. A null first element is treated as absent.</summary>
    public static Option<T> FirstOrNone<T>(this IEnumerable<T> source)
    {
        _ = Guard.NotNull(source, nameof(source));
        using IEnumerator<T> enumerator = source.GetEnumerator();
        return enumerator
            .MoveNext()
            ? enumerator.Current.ToOption()
            : Option<T>.None;
    }
    /// <summary>Returns the first matching element as Some, or None when nothing matches. A null first match is treated as absent.</summary>
    public static Option<T> FirstOrNone<T>(this IEnumerable<T> source, Func<T, bool> predicate)
    {
        _ = Guard.NotNull(source, nameof(source));
        _ = Guard.NotNull(predicate, nameof(predicate));
        return source
        .Where(predicate)
        .FirstOrNone();

    }
    /// <summary>Returns the only element as Some, or None when the sequence is empty. A null element is treated as absent.</summary>
    /// <exception cref="InvalidOperationException">The sequence contains more than one element.</exception>
    public static Option<T> SingleOrNone<T>(this IEnumerable<T> source)
    {
        _ = Guard.NotNull(source, nameof(source));
        using IEnumerator<T> enumerator = source.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            return Option<T>.None;
        }

        T single = enumerator.Current;
        return enumerator.MoveNext()
            ? throw new InvalidOperationException("Sequence contains more than one element.")
            : single.ToOption();
    }
    /// <summary>Returns the only matching element as Some, or None when nothing matches. A null match is treated as absent.</summary>
    /// <exception cref="InvalidOperationException">More than one element matches.</exception>
    public static Option<T> SingleOrNone<T>(this IEnumerable<T> source, Func<T, bool> predicate)
    {
        _ = Guard.NotNull(source, nameof(source));
        _ = Guard.NotNull(predicate, nameof(predicate));
        return source.Where(predicate).SingleOrNone();
    }
    /// <summary>Returns the last element as Some, or None when the sequence is empty. A null last element is treated as absent.</summary>
    public static Option<T> LastOrNone<T>(this IEnumerable<T> source)
    {
        _ = Guard.NotNull(source, nameof(source));
        if (source is IList<T> list)
        {
            return list.Count == 0 ? Option<T>.None : list[list.Count - 1].ToOption();
        }

        using IEnumerator<T> enumerator = source.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            return Option<T>.None;
        }

        T last = enumerator.Current;
        while (enumerator.MoveNext())
        {
            last = enumerator.Current;
        }

        return last.ToOption();
    }
    /// <summary>Returns the last matching element as Some, or None when nothing matches. A null last match is treated as absent.</summary>
    public static Option<T> LastOrNone<T>(this IEnumerable<T> source, Func<T, bool> predicate)
    {
        _ = Guard.NotNull(source, nameof(source));
        _ = Guard.NotNull(predicate, nameof(predicate));
        if (source is IList<T> list)
        {
            for (var index = list.Count - 1; index >= 0; index--)
            {
                if (predicate(list[index]))
                {
                    return list[index].ToOption();
                }
            }

            return Option<T>.None;
        }

        return source.Where(predicate).LastOrNone();
    }
    /// <summary>Returns the value for <paramref name="key"/> as Some, or None when the key is missing. A null value is treated as absent.</summary>
    public static Option<TValue> GetValueOrNone<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key)
    {
        _ = Guard.NotNull(dictionary, nameof(dictionary));
        return dictionary.TryGetValue(key, out TValue? value) ? value.ToOption() : Option<TValue>.None;
    }
    /// <summary>Lazily returns the values of the Some options in <paramref name="source"/>, skipping None.</summary>
    public static IEnumerable<T> Values<T>(this IEnumerable<Option<T>> source)
    {
        _ = Guard.NotNull(source, nameof(source));
        return Iterate(source);

        static IEnumerable<T> Iterate(IEnumerable<Option<T>> options)
        {
            foreach (Option<T> option in options)
            {
                if (option.IsSome)
                {
                    yield return option.Value;
                }
            }
        }
    }
    /// <summary>Creates None for null or Some for a non-null value.</summary>
    public static Option<T> ToOption<T>(this T? value) => value is null ? Option<T>.None : Option<T>.Some(value);
    /// <summary>Creates None for a null nullable value type or Some for its underlying value.</summary>
    public static Option<T> ToOption<T>(this T? value) where T : struct => value.HasValue ? Option<T>.Some(value.Value) : Option<T>.None;
}
