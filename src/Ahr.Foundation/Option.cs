namespace Ahr.Foundation;

/// <summary>Represents a present non-null value or no value.</summary>
public readonly record struct Option<T>
{
    private readonly T? _value;
    private Option(bool isSome, T? value)
    {
        IsSome = isSome;
        _value = value;
    }

    /// <summary>Gets whether a value is present.</summary>
    public bool IsSome
    {
        get;
    }
    /// <summary>Gets whether no value is present.</summary>
    public bool IsNone => !IsSome;
    /// <summary>Gets the present value.</summary>
    public T Value => IsSome ? _value! : throw new InvalidOperationException("Cannot access the value of an empty option.");
    /// <summary>Creates Some with a non-null value.</summary>
    public static Option<T> Some(T value) => new(true, Guard.NotNull(value, nameof(value)));
    /// <summary>Gets None.</summary>
    public static Option<T> None => default;

    /// <summary>Matches the active branch.</summary>
    public TResult Match<TResult>(Func<T, TResult> onSome, Func<TResult> onNone)
    {
        _ = Guard.NotNull(onSome, nameof(onSome));
        _ = Guard.NotNull(onNone, nameof(onNone));
        return IsSome ? onSome(_value!) : onNone();
    }
    /// <summary>Invokes the active branch.</summary>
    public Option<T> Match(Action<T> onSome, Action onNone)
    {
        _ = Guard.NotNull(onSome, nameof(onSome));
        _ = Guard.NotNull(onNone, nameof(onNone));
        if (IsSome)
        {
            onSome(_value!);
        }
        else
        {
            onNone();
        }

        return this;
    }
    /// <summary>Asynchronously matches the active branch.</summary>
    public async Task<TResult> MatchAsync<TResult>(Func<T, Task<TResult>> onSome, Func<Task<TResult>> onNone)
    {
        _ = Guard.NotNull(onSome, nameof(onSome));
        _ = Guard.NotNull(onNone, nameof(onNone));
        Task<TResult> task = IsSome ? onSome(_value!) : onNone();
        return await Guard.NotNull(task, IsSome ? nameof(onSome) : nameof(onNone)).ConfigureAwait(false);
    }
    /// <summary>Asynchronously invokes the active branch.</summary>
    public async Task<Option<T>> MatchAsync(Func<T, Task> onSome, Func<Task> onNone)
    {
        _ = Guard.NotNull(onSome, nameof(onSome));
        _ = Guard.NotNull(onNone, nameof(onNone));
        Task task = IsSome ? onSome(_value!) : onNone();
        await Guard.NotNull(task, IsSome ? nameof(onSome) : nameof(onNone)).ConfigureAwait(false);
        return this;
    }
    /// <summary>Maps a present value.</summary>
    public Option<TOut> Map<TOut>(Func<T, TOut> map)
    {
        _ = Guard.NotNull(map, nameof(map));
        return IsSome ? Option<TOut>.Some(map(_value!)) : Option<TOut>.None;
    }
    /// <summary>Asynchronously maps a present value.</summary>
    public async Task<Option<TOut>> MapAsync<TOut>(Func<T, Task<TOut>> map)
    {
        _ = Guard.NotNull(map, nameof(map));
        return !IsSome ? Option<TOut>.None : Option<TOut>.Some(await Guard.NotNull(map(_value!), nameof(map)).ConfigureAwait(false));
    }
    /// <summary>Binds a present value.</summary>
    public Option<TOut> Bind<TOut>(Func<T, Option<TOut>> bind)
    {
        _ = Guard.NotNull(bind, nameof(bind));
        return IsSome ? bind(_value!) : Option<TOut>.None;
    }
    /// <summary>Asynchronously binds a present value.</summary>
    public async Task<Option<TOut>> BindAsync<TOut>(Func<T, Task<Option<TOut>>> bind)
    {
        _ = Guard.NotNull(bind, nameof(bind));
        return IsSome ? await Guard.NotNull(bind(_value!), nameof(bind)).ConfigureAwait(false) : Option<TOut>.None;
    }
    /// <summary>Filters a present value.</summary>
    public Option<T> Where(Func<T, bool> predicate)
    {
        _ = Guard.NotNull(predicate, nameof(predicate));
        return IsSome && predicate(_value!) ? this : None;
    }
    /// <summary>Asynchronously filters a present value.</summary>
    public async Task<Option<T>> WhereAsync(Func<T, Task<bool>> predicate)
    {
        _ = Guard.NotNull(predicate, nameof(predicate));
        return IsSome && await Guard.NotNull(predicate(_value!), nameof(predicate)).ConfigureAwait(false) ? this : None;
    }
    /// <summary>Invokes an action for a present value.</summary>
    public Option<T> Tap(Action<T> action)
    {
        _ = Guard.NotNull(action, nameof(action));
        if (IsSome)
        {
            action(_value!);
        }

        return this;
    }
    /// <summary>Asynchronously invokes an action for a present value.</summary>
    public async Task<Option<T>> TapAsync(Func<T, Task> action)
    {
        _ = Guard.NotNull(action, nameof(action));
        if (IsSome)
        {
            await Guard.NotNull(action(_value!), nameof(action)).ConfigureAwait(false);
        }

        return this;
    }
    /// <summary>Returns this if present; otherwise returns the eagerly evaluated fallback.</summary>
    public Option<T> OrElse(Option<T> fallback) => IsSome ? this : fallback;
    /// <summary>Returns this if present; otherwise invokes and returns the fallback factory.</summary>
    public Option<T> OrElse(Func<Option<T>> fallback)
    {
        _ = Guard.NotNull(fallback, nameof(fallback));
        return IsSome ? this : fallback();
    }
    /// <summary>Asynchronously returns this if present; otherwise invokes and returns the fallback factory.</summary>
    public async Task<Option<T>> OrElseAsync(Func<Task<Option<T>>> fallback)
    {
        _ = Guard.NotNull(fallback, nameof(fallback));
        return IsSome ? this : await Guard.NotNull(fallback(), nameof(fallback)).ConfigureAwait(false);
    }
    /// <summary>Returns the value or default.</summary>
    public T? GetValueOrDefault() => IsSome ? _value : default;
    /// <summary>Returns the value or a non-null fallback.</summary>
    public T GetValueOrDefault(T fallback) => IsSome ? _value! : Guard.NotNull(fallback, nameof(fallback));
    /// <summary>Converts Some to success and None to failure.</summary>
    public Result<T, TError> ToResult<TError>(TError error)
    {
        _ = Guard.NotNull(error, nameof(error));
        return IsSome ? Result<T, TError>.Success(_value!) : Result<T, TError>.Failure(error);
    }
    /// <summary>Converts Some to success and None to failure.</summary>
    public Result<T> ToResult(Error error)
    {
        _ = Guard.NotNull(error.Message, nameof(error));

        return IsSome ? Result<T>.Success(_value!) : Result<T>.Failure(error);
    }
}
