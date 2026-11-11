using System.Diagnostics;

namespace Ahr.Foundation;

/// <summary>Represents success without a payload or failure with an <see cref="Error"/>.</summary>
[DebuggerDisplay("{ToString(),nq}")]
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
        _ = Guard.ValidError(error, nameof(error));
        return new(ResultState.Failure, error);
    }

    private static readonly Func<Exception, bool> _catchAll = static _ => true;

    // Result<T> never holds null, so a null success gets a message that names the cause instead of a bare ArgumentNullException.
    private static T SuccessFromTry<T>(T value, string method, string parameterName) =>
        value ?? throw new ArgumentNullException(
            parameterName,
            $"The operation passed to {method} returned null, and a result cannot hold null. Return an Option<T> (for example with ToOption()), or convert the value inside the operation.");

    /// <summary>Invokes an operation, converting an exception into a failure with <see cref="Error.FromException(Exception)"/>. <see cref="OperationCanceledException"/> propagates unchanged.</summary>
    public static Result Try(Action operation) => Try(operation, _catchAll);

    /// <summary>Invokes an operation, converting an exception that <paramref name="catchWhen"/> accepts into a failure with <see cref="Error.FromException(Exception)"/>. Other exceptions propagate unchanged, as with a C# <c>when</c> filter. <see cref="OperationCanceledException"/> propagates unchanged.</summary>
    public static Result Try(Action operation, Func<Exception, bool> catchWhen)
    {
        _ = Guard.NotNull(operation, nameof(operation));
        _ = Guard.NotNull(catchWhen, nameof(catchWhen));
        try
        {
            operation();
        }
        catch (Exception ex) when (ex is not OperationCanceledException && catchWhen(ex))
        {
            return ex.ToFailure();
        }

        return Success();
    }

    /// <summary>Invokes an operation, converting a success value into <see cref="Result{T}"/> or an exception into a failure with <see cref="Error.FromException(Exception)"/>. <see cref="OperationCanceledException"/> propagates unchanged.</summary>
    public static Result<T> Try<T>(Func<T> operation) => Try(operation, _catchAll);

    /// <summary>Invokes an operation, converting a success value into <see cref="Result{T}"/> or an exception that <paramref name="catchWhen"/> accepts into a failure with <see cref="Error.FromException(Exception)"/>. Other exceptions propagate unchanged, as with a C# <c>when</c> filter. <see cref="OperationCanceledException"/> propagates unchanged.</summary>
    public static Result<T> Try<T>(Func<T> operation, Func<Exception, bool> catchWhen)
    {
        _ = Guard.NotNull(operation, nameof(operation));
        _ = Guard.NotNull(catchWhen, nameof(catchWhen));
        T value;
        try
        {
            value = operation();
        }
        catch (Exception ex) when (ex is not OperationCanceledException && catchWhen(ex))
        {
            return ex.ToFailure<T>();
        }

        return SuccessFromTry(value, nameof(Try), nameof(operation)).ToSuccess();
    }

    /// <summary>Invokes an operation, converting a success value into <see cref="Result{T, TError}"/> or an exception into a failure produced by <paramref name="errorFactory"/>. <see cref="OperationCanceledException"/> propagates unchanged.</summary>
    public static Result<T, TError> Try<T, TError>(Func<T> operation, Func<Exception, TError> errorFactory) => Try(operation, _catchAll, errorFactory);

    /// <summary>Invokes an operation, converting a success value into <see cref="Result{T, TError}"/> or an exception that <paramref name="catchWhen"/> accepts into a failure produced by <paramref name="errorFactory"/>. Other exceptions propagate unchanged, as with a C# <c>when</c> filter. <see cref="OperationCanceledException"/> propagates unchanged.</summary>
    public static Result<T, TError> Try<T, TError>(Func<T> operation, Func<Exception, bool> catchWhen, Func<Exception, TError> errorFactory)
    {
        _ = Guard.NotNull(operation, nameof(operation));
        _ = Guard.NotNull(catchWhen, nameof(catchWhen));
        _ = Guard.NotNull(errorFactory, nameof(errorFactory));
        T value;
        try
        {
            value = operation();
        }
        catch (Exception ex) when (ex is not OperationCanceledException && catchWhen(ex))
        {
            return Result<T, TError>.Failure(errorFactory(ex));
        }

        return Result<T, TError>.Success(SuccessFromTry(value, nameof(Try), nameof(operation)));
    }

    /// <summary>Asynchronously invokes an operation, converting an exception into a failure with <see cref="Error.FromException(Exception)"/>. <see cref="OperationCanceledException"/> propagates unchanged.</summary>
    public static Task<Result> TryAsync(Func<Task> operation) =>
        TryAsync(operation, _catchAll);

    /// <summary>Asynchronously invokes an operation, converting an exception that <paramref name="catchWhen"/> accepts into a failure with <see cref="Error.FromException(Exception)"/>. Other exceptions propagate unchanged, as with a C# <c>when</c> filter. <see cref="OperationCanceledException"/> propagates unchanged.</summary>
    public static async Task<Result> TryAsync(Func<Task> operation, Func<Exception, bool> catchWhen)
    {
        _ = Guard.NotNull(operation, nameof(operation));
        _ = Guard.NotNull(catchWhen, nameof(catchWhen));

        Task task;
        try
        {
            task = operation();
        }
        catch (Exception ex) when (ex is not OperationCanceledException && catchWhen(ex))
        {
            return ex.ToFailure();
        }

        _ = Guard.NotNull(task, nameof(operation));
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && catchWhen(ex))
        {
            return ex.ToFailure();
        }

        return Success();
    }

    /// <summary>Asynchronously invokes an operation, converting a success value into <see cref="Result{T}"/> or an exception into a failure with <see cref="Error.FromException(Exception)"/>. <see cref="OperationCanceledException"/> propagates unchanged.</summary>
    public static Task<Result<T>> TryAsync<T>(Func<Task<T>> operation) =>
        TryAsync(operation, _catchAll);

    /// <summary>Asynchronously invokes an operation, converting a success value into <see cref="Result{T}"/> or an exception that <paramref name="catchWhen"/> accepts into a failure with <see cref="Error.FromException(Exception)"/>. Other exceptions propagate unchanged, as with a C# <c>when</c> filter. <see cref="OperationCanceledException"/> propagates unchanged.</summary>
    public static async Task<Result<T>> TryAsync<T>(Func<Task<T>> operation, Func<Exception, bool> catchWhen)
    {
        _ = Guard.NotNull(operation, nameof(operation));
        _ = Guard.NotNull(catchWhen, nameof(catchWhen));

        Task<T> task;
        try
        {
            task = operation();
        }
        catch (Exception ex) when (ex is not OperationCanceledException && catchWhen(ex))
        {
            return ex.ToFailure<T>();
        }

        _ = Guard.NotNull(task, nameof(operation));

        T value;
        try
        {
            value = await task.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && catchWhen(ex))
        {
            return ex.ToFailure<T>();
        }

        return SuccessFromTry(value, nameof(TryAsync), nameof(operation)).ToSuccess();
    }

    /// <summary>Asynchronously invokes an operation, converting a success value into <see cref="Result{T, TError}"/> or an exception into a failure produced by <paramref name="errorFactory"/>. <see cref="OperationCanceledException"/> propagates unchanged.</summary>
    public static Task<Result<T, TError>> TryAsync<T, TError>(Func<Task<T>> operation, Func<Exception, TError> errorFactory) =>
        TryAsync(operation, _catchAll, errorFactory);

    /// <summary>Asynchronously invokes an operation, converting a success value into <see cref="Result{T, TError}"/> or an exception that <paramref name="catchWhen"/> accepts into a failure produced by <paramref name="errorFactory"/>. Other exceptions propagate unchanged, as with a C# <c>when</c> filter. <see cref="OperationCanceledException"/> propagates unchanged.</summary>
    public static async Task<Result<T, TError>> TryAsync<T, TError>(Func<Task<T>> operation, Func<Exception, bool> catchWhen, Func<Exception, TError> errorFactory)
    {
        _ = Guard.NotNull(operation, nameof(operation));
        _ = Guard.NotNull(catchWhen, nameof(catchWhen));
        _ = Guard.NotNull(errorFactory, nameof(errorFactory));

        Task<T> task;
        try
        {
            task = operation();
        }
        catch (Exception ex) when (ex is not OperationCanceledException && catchWhen(ex))
        {
            return Result<T, TError>.Failure(errorFactory(ex));
        }

        _ = Guard.NotNull(task, nameof(operation));

        T value;
        try
        {
            value = await task.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && catchWhen(ex))
        {
            return Result<T, TError>.Failure(errorFactory(ex));
        }

        return Result<T, TError>.Success(SuccessFromTry(value, nameof(TryAsync), nameof(operation)));
    }

    /// <summary>Asynchronously invokes a cancellable operation, converting an exception into a failure with <see cref="Error.FromException(Exception)"/>. <see cref="OperationCanceledException"/> propagates only when <paramref name="cancellationToken"/> is canceled; any other cancellation, such as a timeout, becomes a failure.</summary>
    public static Task<Result> TryAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken) =>
        TryAsync(operation, _catchAll, cancellationToken);

    /// <summary>Asynchronously invokes a cancellable operation, converting an exception that <paramref name="catchWhen"/> accepts into a failure with <see cref="Error.FromException(Exception)"/>. Other exceptions propagate unchanged, as with a C# <c>when</c> filter. <see cref="OperationCanceledException"/> propagates only when <paramref name="cancellationToken"/> is canceled; any other cancellation, such as a timeout, becomes a failure without consulting <paramref name="catchWhen"/>.</summary>
    public static async Task<Result> TryAsync(Func<CancellationToken, Task> operation, Func<Exception, bool> catchWhen, CancellationToken cancellationToken)
    {
        _ = Guard.NotNull(operation, nameof(operation));
        _ = Guard.NotNull(catchWhen, nameof(catchWhen));
        cancellationToken.ThrowIfCancellationRequested();

        Task task;
        try
        {
            task = operation(cancellationToken);
        }
        catch (Exception ex) when (ex is OperationCanceledException ? !cancellationToken.IsCancellationRequested : catchWhen(ex))
        {
            return ex.ToFailure();
        }

        _ = Guard.NotNull(task, nameof(operation));
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException ? !cancellationToken.IsCancellationRequested : catchWhen(ex))
        {
            return ex.ToFailure();
        }

        return Success();
    }

    /// <summary>Asynchronously invokes a cancellable operation, converting a success value into <see cref="Result{T}"/> or an exception into a failure with <see cref="Error.FromException(Exception)"/>. <see cref="OperationCanceledException"/> propagates only when <paramref name="cancellationToken"/> is canceled; any other cancellation, such as a timeout, becomes a failure.</summary>
    public static Task<Result<T>> TryAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken) =>
        TryAsync(operation, _catchAll, cancellationToken);

    /// <summary>Asynchronously invokes a cancellable operation, converting a success value into <see cref="Result{T}"/> or an exception that <paramref name="catchWhen"/> accepts into a failure with <see cref="Error.FromException(Exception)"/>. Other exceptions propagate unchanged, as with a C# <c>when</c> filter. <see cref="OperationCanceledException"/> propagates only when <paramref name="cancellationToken"/> is canceled; any other cancellation, such as a timeout, becomes a failure without consulting <paramref name="catchWhen"/>.</summary>
    public static async Task<Result<T>> TryAsync<T>(Func<CancellationToken, Task<T>> operation, Func<Exception, bool> catchWhen, CancellationToken cancellationToken)
    {
        _ = Guard.NotNull(operation, nameof(operation));
        _ = Guard.NotNull(catchWhen, nameof(catchWhen));
        cancellationToken.ThrowIfCancellationRequested();

        Task<T> task;
        try
        {
            task = operation(cancellationToken);
        }
        catch (Exception ex) when (ex is OperationCanceledException ? !cancellationToken.IsCancellationRequested : catchWhen(ex))
        {
            return ex.ToFailure<T>();
        }

        _ = Guard.NotNull(task, nameof(operation));

        T value;
        try
        {
            value = await task.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException ? !cancellationToken.IsCancellationRequested : catchWhen(ex))
        {
            return ex.ToFailure<T>();
        }

        return SuccessFromTry(value, nameof(TryAsync), nameof(operation)).ToSuccess();
    }

    /// <summary>Asynchronously invokes a cancellable operation, converting a success value into <see cref="Result{T, TError}"/> or an exception into a failure produced by <paramref name="errorFactory"/>. <see cref="OperationCanceledException"/> propagates only when <paramref name="cancellationToken"/> is canceled; any other cancellation, such as a timeout, becomes a failure.</summary>
    public static Task<Result<T, TError>> TryAsync<T, TError>(Func<CancellationToken, Task<T>> operation, Func<Exception, TError> errorFactory, CancellationToken cancellationToken) =>
        TryAsync(operation, _catchAll, errorFactory, cancellationToken);

    /// <summary>Asynchronously invokes a cancellable operation, converting a success value into <see cref="Result{T, TError}"/> or an exception that <paramref name="catchWhen"/> accepts into a failure produced by <paramref name="errorFactory"/>. Other exceptions propagate unchanged, as with a C# <c>when</c> filter. <see cref="OperationCanceledException"/> propagates only when <paramref name="cancellationToken"/> is canceled; any other cancellation, such as a timeout, becomes a failure without consulting <paramref name="catchWhen"/>.</summary>
    public static async Task<Result<T, TError>> TryAsync<T, TError>(Func<CancellationToken, Task<T>> operation, Func<Exception, bool> catchWhen, Func<Exception, TError> errorFactory, CancellationToken cancellationToken)
    {
        _ = Guard.NotNull(operation, nameof(operation));
        _ = Guard.NotNull(catchWhen, nameof(catchWhen));
        _ = Guard.NotNull(errorFactory, nameof(errorFactory));
        cancellationToken.ThrowIfCancellationRequested();

        Task<T> task;
        try
        {
            task = operation(cancellationToken);
        }
        catch (Exception ex) when (ex is OperationCanceledException ? !cancellationToken.IsCancellationRequested : catchWhen(ex))
        {
            return Result<T, TError>.Failure(errorFactory(ex));
        }

        _ = Guard.NotNull(task, nameof(operation));

        T value;
        try
        {
            value = await task.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException ? !cancellationToken.IsCancellationRequested : catchWhen(ex))
        {
            return Result<T, TError>.Failure(errorFactory(ex));
        }

        return Result<T, TError>.Success(SuccessFromTry(value, nameof(TryAsync), nameof(operation)));
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

    /// <summary>Invokes a step on success; a failed step becomes the failure.</summary>
    public Result Tap(Func<Result> action)
    {
        _ = Guard.NotNull(action, nameof(action));
        return Bind(action);
    }

    /// <summary>Invokes a step on success; a failed step becomes the failure.</summary>
    public Result Tap<TOut>(Func<Result<TOut>> action)
    {
        _ = Guard.NotNull(action, nameof(action));
        return Bind(() => action().Bind(_ => Success()));
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

    /// <summary>Asynchronously invokes a step on success; a failed step becomes the failure.</summary>
    public Task<Result> TapAsync(Func<Task<Result>> action)
    {
        _ = Guard.NotNull(action, nameof(action));
        return BindAsync(action);
    }

    /// <summary>Asynchronously invokes a step on success; a failed step becomes the failure.</summary>
    public Task<Result> TapAsync<TOut>(Func<Task<Result<TOut>>> action)
    {
        _ = Guard.NotNull(action, nameof(action));
        return BindAsync(async () => (await Guard.NotNull(action(), nameof(action)).ConfigureAwait(false)).Bind(_ => Success()));
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

    /// <summary>Maps a failure error; a success is returned unchanged.</summary>
    public Result MapError(Func<Error, Error> map)
    {
        ThrowIfUninitialized();
        _ = Guard.NotNull(map, nameof(map));
        return _state.IsSuccess ? this : Failure(map(_error));
    }

    /// <summary>Asynchronously maps a failure error; a success is returned unchanged.</summary>
    public async Task<Result> MapErrorAsync(Func<Error, Task<Error>> map)
    {
        ThrowIfUninitialized();
        _ = Guard.NotNull(map, nameof(map));
        return _state.IsSuccess ? this : Failure(await Guard.NotNull(map(_error), nameof(map)).ConfigureAwait(false));
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

    /// <summary>Returns <c>Success</c>, <c>Failure(message)</c>, or <c>Uninitialized</c> without throwing.</summary>
    public override string ToString() => _state switch
    {
        ResultState.Success => "Success",
        ResultState.Failure => $"Failure({_error})",
        ResultState.Uninitialized => "Uninitialized",
        _ => "Uninitialized",
    };

    private void ThrowIfUninitialized() => Guard.ThrowIfUninitialized(_state);
}
