namespace Ahr.Foundation;

/// <summary>Represents success with <typeparamref name="T"/> or failure with <see cref="Error"/>.</summary>
public readonly record struct Result<T>
{
    private readonly Result<T, Error> _inner;
    private Result(Result<T, Error> inner) => _inner = inner;

    /// <summary>Gets whether the result succeeded.</summary>
    public bool IsSuccess => _inner.IsSuccess;
    /// <summary>Gets whether the result failed.</summary>
    public bool IsFailure => _inner.IsFailure;
    /// <summary>Gets the success value.</summary>
    public T Value => _inner.Value;
    /// <summary>Gets the failure error.</summary>
    public Error Error => _inner.Error;

    /// <summary>Creates success with a non-null value.</summary>
    public static Result<T> Success(T value) => new(Result<T, Error>.Success(value));
    /// <summary>Creates failure with a valid error.</summary>
    public static Result<T> Failure(Error error)
    {
        _ = Guard.NotNull(error.Message, nameof(error));
        return new(Result<T, Error>.Failure(error));
    }

    /// <summary>Explicitly converts from the generic error form.</summary>
    public static explicit operator Result<T>(Result<T, Error> result)
    {
        _ = result.IsSuccess;
        return new(result);
    }
    /// <summary>Explicitly converts to the generic error form.</summary>
    public static explicit operator Result<T, Error>(Result<T> result)
    {
        _ = result.IsSuccess;
        return result._inner;
    }

    /// <summary>Matches the active branch.</summary>
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<Error, TResult> onFailure) => _inner.Match(onSuccess, onFailure);
    /// <summary>Invokes the active branch.</summary>
    public Result<T> Match(Action<T> onSuccess, Action<Error> onFailure)
    {
        _ = _inner.Match(onSuccess, onFailure);
        return this;
    }
    /// <summary>Asynchronously matches the active branch.</summary>
    public Task<TResult> MatchAsync<TResult>(Func<T, Task<TResult>> onSuccess, Func<Error, Task<TResult>> onFailure) => _inner.MatchAsync(onSuccess, onFailure);
    /// <summary>Asynchronously invokes the active branch.</summary>
    public async Task<Result<T>> MatchAsync(Func<T, Task> onSuccess, Func<Error, Task> onFailure)
    {
        _ = await _inner.MatchAsync(onSuccess, onFailure).ConfigureAwait(false);
        return this;
    }
    /// <summary>Maps a success.</summary>
    public Result<TOut> Map<TOut>(Func<T, TOut> map) => new(_inner.Map(map));
    /// <summary>Asynchronously maps a success.</summary>
    public async Task<Result<TOut>> MapAsync<TOut>(Func<T, Task<TOut>> map) => new(await _inner.MapAsync(map).ConfigureAwait(false));
    /// <summary>Maps a failure.</summary>
    public Result<T> MapError(Func<Error, Error> map) => new(_inner.MapError(map));
    /// <summary>Asynchronously maps a failure.</summary>
    public async Task<Result<T>> MapErrorAsync(Func<Error, Task<Error>> map) => new(await _inner.MapErrorAsync(map).ConfigureAwait(false));
    /// <summary>Binds a success.</summary>
    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> bind)
    {
        _ = IsSuccess;
        _ = Guard.NotNull(bind, nameof(bind));
        return new(_inner.Bind(value => (Result<TOut, Error>)bind(value)));
    }
    /// <summary>Binds a success to a payload-free result.</summary>
    public Result Bind(Func<T, Result> bind)
    {
        _ = IsSuccess;
        _ = Guard.NotNull(bind, nameof(bind));
        return IsSuccess ? bind(Value) : Result.Failure(Error);
    }
    /// <summary>Asynchronously binds a success.</summary>
    public async Task<Result<TOut>> BindAsync<TOut>(Func<T, Task<Result<TOut>>> bind)
    {
        _ = IsSuccess;
        _ = Guard.NotNull(bind, nameof(bind));
        Result<TOut, Error> value = await _inner.BindAsync(async item => (Result<TOut, Error>)await Guard.NotNull(bind(item), nameof(bind)).ConfigureAwait(false)).ConfigureAwait(false);
        return new(value);
    }
    /// <summary>Asynchronously binds a success to a payload-free result.</summary>
    public async Task<Result> BindAsync(Func<T, Task<Result>> bind)
    {
        _ = IsSuccess;
        _ = Guard.NotNull(bind, nameof(bind));
        return IsSuccess ? await Guard.NotNull(bind(Value), nameof(bind)).ConfigureAwait(false) : Result.Failure(Error);
    }
    /// <summary>Invokes an action on success.</summary>
    public Result<T> Tap(Action<T> action)
    {
        _ = _inner.Tap(action);
        return this;
    }
    /// <summary>Asynchronously invokes an action on success.</summary>
    public async Task<Result<T>> TapAsync(Func<T, Task> action)
    {
        _ = await _inner.TapAsync(action).ConfigureAwait(false);
        return this;
    }
    /// <summary>Invokes an action on failure.</summary>
    public Result<T> TapError(Action<Error> action)
    {
        _ = _inner.TapError(action);
        return this;
    }
    /// <summary>Asynchronously invokes an action on failure.</summary>
    public async Task<Result<T>> TapErrorAsync(Func<Error, Task> action)
    {
        _ = await _inner.TapErrorAsync(action).ConfigureAwait(false);
        return this;
    }
    /// <summary>Ensures a success satisfies a predicate.</summary>
    public Result<T> Ensure(Func<T, bool> predicate, Error error) => new(_inner.Ensure(predicate, error));
    /// <summary>Asynchronously ensures a success satisfies a predicate.</summary>
    public async Task<Result<T>> EnsureAsync(Func<T, Task<bool>> predicate, Error error) => new(await _inner.EnsureAsync(predicate, error).ConfigureAwait(false));
    /// <summary>Returns this on success; otherwise returns the eagerly evaluated fallback.</summary>
    public Result<T> OrElse(Result<T> fallback) => IsSuccess ? this : fallback;
    /// <summary>Returns this on success; otherwise invokes and returns the fallback factory.</summary>
    public Result<T> OrElse(Func<Result<T>> fallback)
    {
        _ = Guard.NotNull(fallback, nameof(fallback));
        return IsSuccess ? this : fallback();
    }
    /// <summary>Asynchronously returns this on success; otherwise invokes and returns the fallback factory.</summary>
    public async Task<Result<T>> OrElseAsync(Func<Task<Result<T>>> fallback)
    {
        _ = Guard.NotNull(fallback, nameof(fallback));
        return IsSuccess ? this : await Guard.NotNull(fallback(), nameof(fallback)).ConfigureAwait(false);
    }
    /// <summary>Converts success to Some and failure to None.</summary>
    public Option<T> ToOption() => _inner.ToOption();
    /// <summary>Returns the success value or default.</summary>
    public T? GetValueOrDefault() => _inner.GetValueOrDefault();
    /// <summary>Returns the success value or a non-null fallback.</summary>
    public T GetValueOrDefault(T fallback) => _inner.GetValueOrDefault(fallback);
}
