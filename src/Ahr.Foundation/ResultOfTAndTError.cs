namespace Ahr.Foundation;

/// <summary>Represents success with <typeparamref name="T"/> or failure with <typeparamref name="TError"/>.</summary>
public readonly record struct Result<T, TError>
{
    private readonly ResultState _state;
    private readonly T? _value;
    private readonly TError? _error;

    private Result(ResultState state, T? value, TError? error)
    {
        _state = state;
        _value = value;
        _error = error;
    }

    /// <summary>Gets whether the result succeeded.</summary>
    public bool IsSuccess
    {
        get
        {
            ThrowIfUninitialized();
            return _state.IsSuccess;
        }
    }
    /// <summary>Gets whether the result failed.</summary>
    public bool IsFailure
    {
        get
        {
            ThrowIfUninitialized();
            return _state.IsFailure;
        }
    }
    /// <summary>Gets the success value.</summary>
    public T Value
    {
        get
        {
            ThrowIfUninitialized();
            return _state.IsSuccess ? _value! : throw new InvalidOperationException("Cannot access the value of a failed result.");
        }
    }
    /// <summary>Gets the failure error.</summary>
    public TError Error
    {
        get
        {
            ThrowIfUninitialized();
            return _state.IsFailure ? _error! : throw new InvalidOperationException("Cannot access the error of a successful result.");
        }
    }

    /// <summary>Creates success with a non-null value.</summary>
    public static Result<T, TError> Success(T value) => new(ResultState.Success, Guard.NotNull(value, nameof(value)), default);
    /// <summary>Creates failure with a non-null error.</summary>
    public static Result<T, TError> Failure(TError error) => new(ResultState.Failure, default, Guard.NotNull(error, nameof(error)));

    /// <summary>Matches the active branch.</summary>
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<TError, TResult> onFailure)
    {
        Validate(onSuccess, nameof(onSuccess));
        _ = Guard.NotNull(onFailure, nameof(onFailure));
        return _state.IsSuccess ? onSuccess(_value!) : onFailure(_error!);
    }

    /// <summary>Invokes the active branch and returns this result.</summary>
    public Result<T, TError> Match(Action<T> onSuccess, Action<TError> onFailure)
    {
        Validate(onSuccess, nameof(onSuccess));
        _ = Guard.NotNull(onFailure, nameof(onFailure));
        if (_state.IsSuccess)
        {
            onSuccess(_value!);
        }
        else
        {
            onFailure(_error!);
        }

        return this;
    }

    /// <summary>Asynchronously matches the active branch.</summary>
    public async Task<TResult> MatchAsync<TResult>(Func<T, Task<TResult>> onSuccess, Func<TError, Task<TResult>> onFailure)
    {
        Validate(onSuccess, nameof(onSuccess));
        _ = Guard.NotNull(onFailure, nameof(onFailure));
        Task<TResult> task = _state.IsSuccess ? onSuccess(_value!) : onFailure(_error!);
        return await Guard.NotNull(task, _state.IsSuccess ? nameof(onSuccess) : nameof(onFailure)).ConfigureAwait(false);
    }

    /// <summary>Asynchronously invokes the active branch and returns this result.</summary>
    public async Task<Result<T, TError>> MatchAsync(Func<T, Task> onSuccess, Func<TError, Task> onFailure)
    {
        Validate(onSuccess, nameof(onSuccess));
        _ = Guard.NotNull(onFailure, nameof(onFailure));
        Task task = _state.IsSuccess ? onSuccess(_value!) : onFailure(_error!);
        await Guard.NotNull(task, _state.IsSuccess ? nameof(onSuccess) : nameof(onFailure)).ConfigureAwait(false);
        return this;
    }

    /// <summary>Maps a success value.</summary>
    public Result<TOut, TError> Map<TOut>(Func<T, TOut> map)
    {
        Validate(map, nameof(map));
        return _state.IsSuccess ? Result<TOut, TError>.Success(map(_value!)) : Result<TOut, TError>.Failure(_error!);
    }

    /// <summary>Asynchronously maps a success value.</summary>
    public async Task<Result<TOut, TError>> MapAsync<TOut>(Func<T, Task<TOut>> map)
    {
        Validate(map, nameof(map));
        if (_state.IsFailure)
        {
            return Result<TOut, TError>.Failure(_error!);
        }

        TOut? mapped = await Guard.NotNull(map(_value!), nameof(map)).ConfigureAwait(false);
        return Result<TOut, TError>.Success(mapped);
    }

    /// <summary>Maps a failure error.</summary>
    public Result<T, TOutError> MapError<TOutError>(Func<TError, TOutError> map)
    {
        Validate(map, nameof(map));
        return _state.IsSuccess ? Result<T, TOutError>.Success(_value!) : Result<T, TOutError>.Failure(map(_error!));
    }

    /// <summary>Asynchronously maps a failure error.</summary>
    public async Task<Result<T, TOutError>> MapErrorAsync<TOutError>(Func<TError, Task<TOutError>> map)
    {
        Validate(map, nameof(map));
        if (_state.IsSuccess)
        {
            return Result<T, TOutError>.Success(_value!);
        }

        TOutError? mapped = await Guard.NotNull(map(_error!), nameof(map)).ConfigureAwait(false);
        return Result<T, TOutError>.Failure(mapped);
    }

    /// <summary>Binds a success value.</summary>
    public Result<TOut, TError> Bind<TOut>(Func<T, Result<TOut, TError>> bind)
    {
        Validate(bind, nameof(bind));
        return _state.IsSuccess ? bind(_value!) : Result<TOut, TError>.Failure(_error!);
    }

    /// <summary>Asynchronously binds a success value.</summary>
    public async Task<Result<TOut, TError>> BindAsync<TOut>(Func<T, Task<Result<TOut, TError>>> bind)
    {
        Validate(bind, nameof(bind));
        return _state.IsFailure
            ? Result<TOut, TError>.Failure(_error!)
            : await Guard.NotNull(bind(_value!), nameof(bind)).ConfigureAwait(false);
    }

    /// <summary>Invokes an action on success.</summary>
    public Result<T, TError> Tap(Action<T> action)
    {
        Validate(action, nameof(action));
        if (_state.IsSuccess)
        {
            action(_value!);
        }

        return this;
    }

    /// <summary>Asynchronously invokes an action on success.</summary>
    public async Task<Result<T, TError>> TapAsync(Func<T, Task> action)
    {
        Validate(action, nameof(action));
        if (_state.IsSuccess)
        {
            await Guard.NotNull(action(_value!), nameof(action)).ConfigureAwait(false);
        }

        return this;
    }

    /// <summary>Invokes an action on failure.</summary>
    public Result<T, TError> TapError(Action<TError> action)
    {
        Validate(action, nameof(action));
        if (_state.IsFailure)
        {
            action(_error!);
        }

        return this;
    }

    /// <summary>Asynchronously invokes an action on failure.</summary>
    public async Task<Result<T, TError>> TapErrorAsync(Func<TError, Task> action)
    {
        Validate(action, nameof(action));
        if (_state.IsFailure)
        {
            await Guard.NotNull(action(_error!), nameof(action)).ConfigureAwait(false);
        }

        return this;
    }

    /// <summary>Converts a success to failure when a predicate is false.</summary>
    public Result<T, TError> Ensure(Func<T, bool> predicate, TError error)
    {
        Validate(predicate, nameof(predicate));
        _ = Guard.NotNull(error, nameof(error));
        return _state.IsSuccess && !predicate(_value!) ? Failure(error) : this;
    }

    /// <summary>Asynchronously converts a success to failure when a predicate is false.</summary>
    public async Task<Result<T, TError>> EnsureAsync(Func<T, Task<bool>> predicate, TError error)
    {
        Validate(predicate, nameof(predicate));
        _ = Guard.NotNull(error, nameof(error));
        return _state.IsSuccess && !await Guard.NotNull(predicate(_value!), nameof(predicate)).ConfigureAwait(false)
            ? Failure(error)
            : this;
    }

    /// <summary>Returns this on success; otherwise returns the eagerly evaluated fallback.</summary>
    public Result<T, TError> OrElse(Result<T, TError> fallback)
    {
        ThrowIfUninitialized();
        return _state.IsSuccess ? this : fallback;
    }

    /// <summary>Returns this on success; otherwise invokes and returns the fallback factory.</summary>
    public Result<T, TError> OrElse(Func<Result<T, TError>> fallback)
    {
        Validate(fallback, nameof(fallback));
        return _state.IsSuccess ? this : fallback();
    }

    /// <summary>Asynchronously returns this on success; otherwise invokes and returns the fallback factory.</summary>
    public async Task<Result<T, TError>> OrElseAsync(Func<Task<Result<T, TError>>> fallback)
    {
        Validate(fallback, nameof(fallback));
        return _state.IsSuccess ? this : await Guard.NotNull(fallback(), nameof(fallback)).ConfigureAwait(false);
    }

    /// <summary>Converts success to Some and failure to None.</summary>
    public Option<T> ToOption()
    {
        ThrowIfUninitialized();
        return _state.IsSuccess ? Option<T>.Some(_value!) : Option<T>.None;
    }

    /// <summary>Returns the success value or default.</summary>
    public T? GetValueOrDefault()
    {
        ThrowIfUninitialized();
        return _state.IsSuccess ? _value : default;
    }

    /// <summary>Returns the success value or a non-null fallback.</summary>
    public T GetValueOrDefault(T fallback)
    {
        ThrowIfUninitialized();
        return _state.IsSuccess ? _value! : Guard.NotNull(fallback, nameof(fallback));
    }

    private void Validate<TDelegate>(TDelegate value, string name)
    {
        ThrowIfUninitialized();
        _ = Guard.NotNull(value, name);
    }
    private void ThrowIfUninitialized() => Guard.ThrowIfUninitialized(_state);
}
