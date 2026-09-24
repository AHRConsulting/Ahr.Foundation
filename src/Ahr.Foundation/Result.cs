namespace Ahr.Foundation;

/// <summary>Represents success without a payload or failure with an <see cref="Error"/>.</summary>
public readonly record struct Result
{
    private readonly ResultState _state;
    private readonly Error _error;

    private Result(ResultState state, Error error)
    {
        _state = state;
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

    /// <summary>Gets the failure error.</summary>
    public Error Error
    {
        get
        {
            ThrowIfUninitialized();
            return _state.IsFailure
                ? _error
                : throw new InvalidOperationException("Cannot access the error of a successful result.");
        }
    }

    /// <summary>Creates success.</summary>
    public static Result Success() => new(ResultState.Success, default);

    /// <summary>Creates failure.</summary>
    public static Result Failure(Error error)
    {
        _ = Guard.NotNull(error.Message, nameof(error));
        return new(ResultState.Failure, error);
    }

    /// <summary>Invokes an operation, converting an exception into a failure with <see cref="Error.FromException(Exception)"/>. <see cref="OperationCanceledException"/> propagates unchanged.</summary>
    public static Result Try(Action operation)
    {
        _ = Guard.NotNull(operation, nameof(operation));
        try
        {
            operation();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ex.ToFailure();
        }

        return Success();
    }

    /// <summary>Invokes an operation, converting a success value into <see cref="Result{T}"/> or an exception into a failure with <see cref="Error.FromException(Exception)"/>. <see cref="OperationCanceledException"/> propagates unchanged.</summary>
    public static Result<T> Try<T>(Func<T> operation)
    {
        _ = Guard.NotNull(operation, nameof(operation));
        T value;
        try
        {
            value = operation();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ex.ToFailure<T>();
        }

        return value.ToSuccess();
    }

    /// <summary>Invokes an operation, converting a success value into <see cref="Result{T, TError}"/> or an exception into a failure produced by <paramref name="errorFactory"/>. <see cref="OperationCanceledException"/> propagates unchanged.</summary>
    public static Result<T, TError> Try<T, TError>(Func<T> operation, Func<Exception, TError> errorFactory)
    {
        _ = Guard.NotNull(operation, nameof(operation));
        _ = Guard.NotNull(errorFactory, nameof(errorFactory));
        T value;
        try
        {
            value = operation();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<T, TError>.Failure(errorFactory(ex));
        }

        return Result<T, TError>.Success(value);
    }

    /// <summary>Asynchronously invokes an operation, converting an exception into a failure with <see cref="Error.FromException(Exception)"/>. <see cref="OperationCanceledException"/> propagates unchanged.</summary>
    public static async Task<Result> TryAsync(Func<Task> operation)
    {
        _ = Guard.NotNull(operation, nameof(operation));

        Task task;
        try
        {
            task = operation();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ex.ToFailure();
        }

        _ = Guard.NotNull(task, nameof(operation));

        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ex.ToFailure();
        }

        return Success();
    }

    /// <summary>Asynchronously invokes an operation, converting a success value into <see cref="Result{T}"/> or an exception into a failure with <see cref="Error.FromException(Exception)"/>. <see cref="OperationCanceledException"/> propagates unchanged.</summary>
    public static async Task<Result<T>> TryAsync<T>(Func<Task<T>> operation)
    {
        _ = Guard.NotNull(operation, nameof(operation));

        Task<T> task;
        try
        {
            task = operation();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ex.ToFailure<T>();
        }

        _ = Guard.NotNull(task, nameof(operation));

        T value;
        try
        {
            value = await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ex.ToFailure<T>();
        }

        return value.ToSuccess();
    }

    /// <summary>Asynchronously invokes an operation, converting a success value into <see cref="Result{T, TError}"/> or an exception into a failure produced by <paramref name="errorFactory"/>. <see cref="OperationCanceledException"/> propagates unchanged.</summary>
    public static async Task<Result<T, TError>> TryAsync<T, TError>(Func<Task<T>> operation, Func<Exception, TError> errorFactory)
    {
        _ = Guard.NotNull(operation, nameof(operation));
        _ = Guard.NotNull(errorFactory, nameof(errorFactory));

        Result<T, TError> FromException(Exception exception) =>
            Result<T, TError>.Failure(errorFactory(exception));

        Task<T> task;
        try
        {
            task = operation();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return FromException(ex);
        }

        _ = Guard.NotNull(task, nameof(operation));

        T value;
        try
        {
            value = await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return FromException(ex);
        }

        return Result<T, TError>.Success(value);
    }

    /// <summary>Matches the active branch.</summary>
    public TResult Match<TResult>(Func<TResult> onSuccess, Func<Error, TResult> onFailure)
    {
        ThrowIfUninitialized();
        _ = Guard.NotNull(onSuccess, nameof(onSuccess));
        _ = Guard.NotNull(onFailure, nameof(onFailure));
        return _state.IsSuccess ? onSuccess() : onFailure(_error);
    }

    /// <summary>Invokes the active branch and returns this result.</summary>
    public Result Match(Action onSuccess, Action<Error> onFailure)
    {
        ThrowIfUninitialized();
        _ = Guard.NotNull(onSuccess, nameof(onSuccess));
        _ = Guard.NotNull(onFailure, nameof(onFailure));
        if (_state.IsSuccess)
        {
            onSuccess();
        }
        else
        {
            onFailure(_error);
        }

        return this;
    }

    /// <summary>Asynchronously matches the active branch.</summary>
    public async Task<TResult> MatchAsync<TResult>(Func<Task<TResult>> onSuccess, Func<Error, Task<TResult>> onFailure)
    {
        ThrowIfUninitialized();
        _ = Guard.NotNull(onSuccess, nameof(onSuccess));
        _ = Guard.NotNull(onFailure, nameof(onFailure));
        Task<TResult> task = _state.IsSuccess ? onSuccess() : onFailure(_error);
        return await Guard.NotNull(task, _state.IsSuccess ? nameof(onSuccess) : nameof(onFailure)).ConfigureAwait(false);
    }

    /// <summary>Asynchronously invokes the active branch and returns this result.</summary>
    public async Task<Result> MatchAsync(Func<Task> onSuccess, Func<Error, Task> onFailure)
    {
        ThrowIfUninitialized();
        _ = Guard.NotNull(onSuccess, nameof(onSuccess));
        _ = Guard.NotNull(onFailure, nameof(onFailure));
        Task task = _state.IsSuccess ? onSuccess() : onFailure(_error);
        await Guard.NotNull(task, _state.IsSuccess ? nameof(onSuccess) : nameof(onFailure)).ConfigureAwait(false);
        return this;
    }

    /// <summary>Binds a success to another result.</summary>
    public Result Bind(Func<Result> bind)
    {
        ThrowIfUninitialized();
        _ = Guard.NotNull(bind, nameof(bind));
        return _state.IsSuccess ? bind() : this;
    }

    /// <summary>Binds a success to a value result.</summary>
    public Result<TOut> Bind<TOut>(Func<Result<TOut>> bind)
    {
        ThrowIfUninitialized();
        _ = Guard.NotNull(bind, nameof(bind));
        return _state.IsSuccess ? bind() : Result<TOut>.Failure(_error);
    }

    /// <summary>Asynchronously binds a success.</summary>
    public async Task<Result> BindAsync(Func<Task<Result>> bind)
    {
        ThrowIfUninitialized();
        _ = Guard.NotNull(bind, nameof(bind));
        return _state.IsFailure ? this : await Guard.NotNull(bind(), nameof(bind)).ConfigureAwait(false);
    }

    /// <summary>Asynchronously binds a success to a value result.</summary>
    public async Task<Result<TOut>> BindAsync<TOut>(Func<Task<Result<TOut>>> bind)
    {
        ThrowIfUninitialized();
        _ = Guard.NotNull(bind, nameof(bind));
        return _state.IsFailure ? Result<TOut>.Failure(_error) : await Guard.NotNull(bind(), nameof(bind)).ConfigureAwait(false);
    }

    /// <summary>Invokes an action on success.</summary>
    public Result Tap(Action action)
    {
        ThrowIfUninitialized();
        _ = Guard.NotNull(action, nameof(action));
        if (_state.IsSuccess)
        {
            action();
        }

        return this;
    }

    /// <summary>Asynchronously invokes an action on success.</summary>
    public async Task<Result> TapAsync(Func<Task> action)
    {
        ThrowIfUninitialized();
        _ = Guard.NotNull(action, nameof(action));
        if (_state.IsSuccess)
        {
            await Guard.NotNull(action(), nameof(action)).ConfigureAwait(false);
        }

        return this;
    }

    /// <summary>Invokes an action on failure.</summary>
    public Result TapError(Action<Error> action)
    {
        ThrowIfUninitialized();
        _ = Guard.NotNull(action, nameof(action));
        if (_state.IsFailure)
        {
            action(_error);
        }

        return this;
    }

    /// <summary>Asynchronously invokes an action on failure.</summary>
    public async Task<Result> TapErrorAsync(Func<Error, Task> action)
    {
        ThrowIfUninitialized();
        _ = Guard.NotNull(action, nameof(action));
        if (_state.IsFailure)
        {
            await Guard.NotNull(action(_error), nameof(action)).ConfigureAwait(false);
        }

        return this;
    }

    /// <summary>Returns this on success; otherwise returns the eagerly evaluated fallback.</summary>
    public Result OrElse(Result fallback)
    {
        ThrowIfUninitialized();
        return _state.IsSuccess ? this : fallback;
    }

    /// <summary>Returns this on success; otherwise invokes and returns the fallback factory.</summary>
    public Result OrElse(Func<Result> fallback)
    {
        ThrowIfUninitialized();
        _ = Guard.NotNull(fallback, nameof(fallback));
        return _state.IsSuccess ? this : fallback();
    }

    /// <summary>Asynchronously returns this on success; otherwise invokes and returns the fallback factory.</summary>
    public async Task<Result> OrElseAsync(Func<Task<Result>> fallback)
    {
        ThrowIfUninitialized();
        _ = Guard.NotNull(fallback, nameof(fallback));
        return _state.IsSuccess ? this : await Guard.NotNull(fallback(), nameof(fallback)).ConfigureAwait(false);
    }

    private void ThrowIfUninitialized() => Guard.ThrowIfUninitialized(_state);
}
