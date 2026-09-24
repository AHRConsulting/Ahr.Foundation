namespace Ahr.Foundation;

/// <summary>
/// LINQ query-syntax support for <see cref="Result{T}"/>, <see cref="Result{T, TError}"/>, and
/// <see cref="Option{T}"/>.
/// </summary>
/// <remarks>
/// <c>Select</c> maps a success value and <c>SelectMany</c> chains a dependent operation, so
/// query syntax such as
/// <c>from user in FindUser(id) from order in LoadOrder(user) select order.Total</c>
/// short-circuits on the first failure exactly as the equivalent <c>Bind</c> chain does.
/// Prefer <c>Map</c> and <c>Bind</c> for short chains; query syntax suits pipelines where later
/// steps need values bound earlier. Query syntax is synchronous only.
/// </remarks>
public static class LinqExtensions
{
    /// <summary>Projects a successful value. Query-syntax alias for <see cref="Result{T}.Map"/>.</summary>
    public static Result<TOut> Select<T, TOut>(this Result<T> result, Func<T, TOut> selector) =>
        result.Map(selector);

    /// <summary>Projects a successful value. Query-syntax alias for <see cref="Result{T, TError}.Map"/>.</summary>
    public static Result<TOut, TError> Select<T, TError, TOut>(this Result<T, TError> result, Func<T, TOut> selector) =>
        result.Map(selector);

    /// <summary>Projects a present value. Query-syntax alias for <see cref="Option{T}.Map"/>.</summary>
    public static Option<TOut> Select<T, TOut>(this Option<T> option, Func<T, TOut> selector) =>
        option.Map(selector);

    /// <summary>Chains a dependent result and projects both values, enabling multi-clause queries.</summary>
    public static Result<TOut> SelectMany<T, TIntermediate, TOut>(
        this Result<T> result,
        Func<T, Result<TIntermediate>> selector,
        Func<T, TIntermediate, TOut> resultSelector)
    {
        _ = Guard.NotNull(selector, nameof(selector));
        _ = Guard.NotNull(resultSelector, nameof(resultSelector));
        return result.Bind(value => selector(value).Map(intermediate => resultSelector(value, intermediate)));
    }

    /// <summary>Chains a dependent result and projects both values, enabling multi-clause queries.</summary>
    public static Result<TOut, TError> SelectMany<T, TError, TIntermediate, TOut>(
        this Result<T, TError> result,
        Func<T, Result<TIntermediate, TError>> selector,
        Func<T, TIntermediate, TOut> resultSelector)
    {
        _ = Guard.NotNull(selector, nameof(selector));
        _ = Guard.NotNull(resultSelector, nameof(resultSelector));
        return result.Bind(value => selector(value).Map(intermediate => resultSelector(value, intermediate)));
    }

    /// <summary>Chains a dependent option and projects both values, enabling multi-clause queries.</summary>
    public static Option<TOut> SelectMany<T, TIntermediate, TOut>(
        this Option<T> option,
        Func<T, Option<TIntermediate>> selector,
        Func<T, TIntermediate, TOut> resultSelector)
    {
        _ = Guard.NotNull(selector, nameof(selector));
        _ = Guard.NotNull(resultSelector, nameof(resultSelector));
        return option.Bind(value => selector(value).Map(intermediate => resultSelector(value, intermediate)));
    }
}
