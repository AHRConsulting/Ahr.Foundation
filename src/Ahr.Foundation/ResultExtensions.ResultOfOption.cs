namespace Ahr.Foundation;

public static partial class ResultExtensions
{
    extension<T>(Result<Option<T>> result)
    {
        /// <summary>Matches a found value, a missing value, or a failure without nesting.</summary>
        public TResult Match<TResult>(Func<T, TResult> onSome, Func<TResult> onNone, Func<Error, TResult> onFailure)
        {
            _ = Guard.NotNull(onSome, nameof(onSome));
            _ = Guard.NotNull(onNone, nameof(onNone));
            _ = Guard.NotNull(onFailure, nameof(onFailure));
            return result.Match(option => option.Match(onSome, onNone), onFailure);
        }

        /// <summary>Asynchronously matches a found value, a missing value, or a failure without nesting.</summary>
        public async Task<TResult> MatchAsync<TResult>(Func<T, Task<TResult>> onSome, Func<Task<TResult>> onNone, Func<Error, Task<TResult>> onFailure)
        {
            _ = Guard.NotNull(onSome, nameof(onSome));
            _ = Guard.NotNull(onNone, nameof(onNone));
            _ = Guard.NotNull(onFailure, nameof(onFailure));
            return await result.MatchAsync(option => option.MatchAsync(onSome, onNone), onFailure).ConfigureAwait(false);
        }

        /// <summary>Runs the action for a found value, a missing value, or a failure without nesting, and returns the original result.</summary>
        public Result<Option<T>> Match(Action<T> onSome, Action onNone, Action<Error> onFailure)
        {
            _ = Guard.NotNull(onSome, nameof(onSome));
            _ = Guard.NotNull(onNone, nameof(onNone));
            _ = Guard.NotNull(onFailure, nameof(onFailure));
            return result.Match(OnValue, onFailure);

            void OnValue(Option<T> option) => _ = option.Match(onSome, onNone);
        }

        /// <summary>Asynchronously runs the action for a found value, a missing value, or a failure without nesting, and returns the original result.</summary>
        public async Task<Result<Option<T>>> MatchAsync(Func<T, Task> onSome, Func<Task> onNone, Func<Error, Task> onFailure)
        {
            _ = Guard.NotNull(onSome, nameof(onSome));
            _ = Guard.NotNull(onNone, nameof(onNone));
            _ = Guard.NotNull(onFailure, nameof(onFailure));
            return await result.MatchAsync(OnValue, onFailure).ConfigureAwait(false);

            Task OnValue(Option<T> option) => option.MatchAsync(onSome, onNone);
        }

        /// <summary>Runs <paramref name="action"/> for a found value and returns the original result; a missing value and a failure pass through.</summary>
        public Result<Option<T>> TapSome(Action<T> action)
        {
            _ = Guard.NotNull(action, nameof(action));
            return result.Match(OnValue, static _ => { });

            void OnValue(Option<T> option) => _ = option.Tap(action);
        }

        /// <summary>Asynchronously runs <paramref name="action"/> for a found value and returns the original result; a missing value and a failure pass through.</summary>
        public async Task<Result<Option<T>>> TapSomeAsync(Func<T, Task> action)
        {
            _ = Guard.NotNull(action, nameof(action));
            return await result.MatchAsync(OnValue, static _ => Task.CompletedTask).ConfigureAwait(false);

            Task OnValue(Option<T> option) => option.TapAsync(action);
        }

        /// <summary>Runs <paramref name="action"/> for a found value and keeps the original result; a failure from <paramref name="action"/> stops the pipeline.</summary>
        public Result<Option<T>> TapSome(Func<T, Result> action)
        {
            _ = Guard.NotNull(action, nameof(action));
            Result<Option<T>> original = result;
            return result.Bind(option => option.IsSome ? action(option.Value).Bind(() => original) : original);
        }

        /// <summary>Runs <paramref name="action"/> for a found value and keeps the original result; a failure from <paramref name="action"/> stops the pipeline.</summary>
        public Result<Option<T>> TapSome<TOut>(Func<T, Result<TOut>> action)
        {
            _ = Guard.NotNull(action, nameof(action));
            Result<Option<T>> original = result;
            return result.Bind(option => option.IsSome ? action(option.Value).Bind(_ => original) : original);
        }

        /// <summary>Asynchronously runs <paramref name="action"/> for a found value and keeps the original result; a failure from <paramref name="action"/> stops the pipeline.</summary>
        public async Task<Result<Option<T>>> TapSomeAsync(Func<T, Task<Result>> action)
        {
            _ = Guard.NotNull(action, nameof(action));
            Result<Option<T>> original = result;
            return await result.BindAsync(OnValue).ConfigureAwait(false);

            async Task<Result<Option<T>>> OnValue(Option<T> option) =>
                option.IsSome ? (await action(option.Value).ConfigureAwait(false)).Bind(() => original) : original;
        }

        /// <summary>Asynchronously runs <paramref name="action"/> for a found value and keeps the original result; a failure from <paramref name="action"/> stops the pipeline.</summary>
        public async Task<Result<Option<T>>> TapSomeAsync<TOut>(Func<T, Task<Result<TOut>>> action)
        {
            _ = Guard.NotNull(action, nameof(action));
            Result<Option<T>> original = result;
            return await result.BindAsync(OnValue).ConfigureAwait(false);

            async Task<Result<Option<T>>> OnValue(Option<T> option) =>
                option.IsSome ? (await action(option.Value).ConfigureAwait(false)).Bind(_ => original) : original;
        }

        /// <summary>Unwraps Some and turns None into <paramref name="fallback"/>; an existing failure is kept.</summary>
        public Result<T> SomeOr(T fallback)
        {
            _ = Guard.NotNull(fallback, nameof(fallback));
            return result.Map(option => option.IsSome ? option.Value : fallback);
        }

        /// <summary>Continues with another lookup when no value was found; a found value and a failure pass through.</summary>
        public Result<Option<T>> BindNone(Func<Result<Option<T>>> bind)
        {
            _ = Guard.NotNull(bind, nameof(bind));
            Result<Option<T>> original = result;
            return result.Bind(option => option.IsNone ? bind() : original);
        }

        /// <summary>Asynchronously continues with another lookup when no value was found; a found value and a failure pass through.</summary>
        public async Task<Result<Option<T>>> BindNoneAsync(Func<Task<Result<Option<T>>>> bind)
        {
            _ = Guard.NotNull(bind, nameof(bind));
            Result<Option<T>> original = result;
            return await result.BindAsync(option => option.IsNone ? bind() : Task.FromResult(original)).ConfigureAwait(false);
        }

        /// <summary>Unwraps Some and turns None into a failure with <paramref name="error"/>; an existing failure is kept.</summary>
        public Result<T> EnsureSome(Error error)
        {
            _ = Guard.ValidError(error, nameof(error));
            return result.Bind(option => option.ToResult(error));
        }

        /// <summary>Maps a found value; a missing value and a failure pass through.</summary>
        public Result<Option<TOut>> MapSome<TOut>(Func<T, TOut> map)
        {
            _ = Guard.NotNull(map, nameof(map));
            return result.Map(option => option.Map(map));
        }

        /// <summary>Asynchronously maps a found value; a missing value and a failure pass through.</summary>
        public async Task<Result<Option<TOut>>> MapSomeAsync<TOut>(Func<T, Task<TOut>> map)
        {
            _ = Guard.NotNull(map, nameof(map));
            return await result.MapAsync(option => option.MapAsync(map)).ConfigureAwait(false);
        }

        /// <summary>Continues with another lookup for a found value; a missing value and a failure pass through.</summary>
        public Result<Option<TOut>> BindSome<TOut>(Func<T, Result<Option<TOut>>> bind)
        {
            _ = Guard.NotNull(bind, nameof(bind));
            return result.Bind(option => option.Match(bind, () => Result<Option<TOut>>.Success(Option<TOut>.None)));
        }

        /// <summary>Asynchronously continues with another lookup for a found value; a missing value and a failure pass through.</summary>
        public async Task<Result<Option<TOut>>> BindSomeAsync<TOut>(Func<T, Task<Result<Option<TOut>>>> bind)
        {
            _ = Guard.NotNull(bind, nameof(bind));
            return await result.BindAsync(option => option.MatchAsync(bind, () => Task.FromResult(Result<Option<TOut>>.Success(Option<TOut>.None)))).ConfigureAwait(false);
        }
    }

    extension<T, TError>(Result<Option<T>, TError> result)
    {
        /// <summary>Matches a found value, a missing value, or a failure without nesting.</summary>
        public TResult Match<TResult>(Func<T, TResult> onSome, Func<TResult> onNone, Func<TError, TResult> onFailure)
        {
            _ = Guard.NotNull(onSome, nameof(onSome));
            _ = Guard.NotNull(onNone, nameof(onNone));
            _ = Guard.NotNull(onFailure, nameof(onFailure));
            return result.Match(option => option.Match(onSome, onNone), onFailure);
        }

        /// <summary>Asynchronously matches a found value, a missing value, or a failure without nesting.</summary>
        public async Task<TResult> MatchAsync<TResult>(Func<T, Task<TResult>> onSome, Func<Task<TResult>> onNone, Func<TError, Task<TResult>> onFailure)
        {
            _ = Guard.NotNull(onSome, nameof(onSome));
            _ = Guard.NotNull(onNone, nameof(onNone));
            _ = Guard.NotNull(onFailure, nameof(onFailure));
            return await result.MatchAsync(option => option.MatchAsync(onSome, onNone), onFailure).ConfigureAwait(false);
        }

        /// <summary>Runs the action for a found value, a missing value, or a failure without nesting, and returns the original result.</summary>
        public Result<Option<T>, TError> Match(Action<T> onSome, Action onNone, Action<TError> onFailure)
        {
            _ = Guard.NotNull(onSome, nameof(onSome));
            _ = Guard.NotNull(onNone, nameof(onNone));
            _ = Guard.NotNull(onFailure, nameof(onFailure));
            return result.Match(OnValue, onFailure);

            void OnValue(Option<T> option) => _ = option.Match(onSome, onNone);
        }

        /// <summary>Asynchronously runs the action for a found value, a missing value, or a failure without nesting, and returns the original result.</summary>
        public async Task<Result<Option<T>, TError>> MatchAsync(Func<T, Task> onSome, Func<Task> onNone, Func<TError, Task> onFailure)
        {
            _ = Guard.NotNull(onSome, nameof(onSome));
            _ = Guard.NotNull(onNone, nameof(onNone));
            _ = Guard.NotNull(onFailure, nameof(onFailure));
            return await result.MatchAsync(OnValue, onFailure).ConfigureAwait(false);

            Task OnValue(Option<T> option) => option.MatchAsync(onSome, onNone);
        }

        /// <summary>Runs <paramref name="action"/> for a found value and returns the original result; a missing value and a failure pass through.</summary>
        public Result<Option<T>, TError> TapSome(Action<T> action)
        {
            _ = Guard.NotNull(action, nameof(action));
            return result.Match(OnValue, static _ => { });

            void OnValue(Option<T> option) => _ = option.Tap(action);
        }

        /// <summary>Asynchronously runs <paramref name="action"/> for a found value and returns the original result; a missing value and a failure pass through.</summary>
        public async Task<Result<Option<T>, TError>> TapSomeAsync(Func<T, Task> action)
        {
            _ = Guard.NotNull(action, nameof(action));
            return await result.MatchAsync(OnValue, static _ => Task.CompletedTask).ConfigureAwait(false);

            Task OnValue(Option<T> option) => option.TapAsync(action);
        }

        /// <summary>Runs <paramref name="action"/> for a found value and keeps the original result; a failure from <paramref name="action"/> stops the pipeline.</summary>
        public Result<Option<T>, TError> TapSome<TOut>(Func<T, Result<TOut, TError>> action)
        {
            _ = Guard.NotNull(action, nameof(action));
            Result<Option<T>, TError> original = result;
            return result.Bind(option => option.IsSome ? action(option.Value).Bind(_ => original) : original);
        }

        /// <summary>Asynchronously runs <paramref name="action"/> for a found value and keeps the original result; a failure from <paramref name="action"/> stops the pipeline.</summary>
        public async Task<Result<Option<T>, TError>> TapSomeAsync<TOut>(Func<T, Task<Result<TOut, TError>>> action)
        {
            _ = Guard.NotNull(action, nameof(action));
            Result<Option<T>, TError> original = result;
            return await result.BindAsync(OnValue).ConfigureAwait(false);

            async Task<Result<Option<T>, TError>> OnValue(Option<T> option) =>
                option.IsSome ? (await action(option.Value).ConfigureAwait(false)).Bind(_ => original) : original;
        }

        /// <summary>Unwraps Some and turns None into <paramref name="fallback"/>; an existing failure is kept.</summary>
        public Result<T, TError> SomeOr(T fallback)
        {
            _ = Guard.NotNull(fallback, nameof(fallback));
            return result.Map(option => option.IsSome ? option.Value : fallback);
        }

        /// <summary>Continues with another lookup when no value was found; a found value and a failure pass through.</summary>
        public Result<Option<T>, TError> BindNone(Func<Result<Option<T>, TError>> bind)
        {
            _ = Guard.NotNull(bind, nameof(bind));
            Result<Option<T>, TError> original = result;
            return result.Bind(option => option.IsNone ? bind() : original);
        }

        /// <summary>Asynchronously continues with another lookup when no value was found; a found value and a failure pass through.</summary>
        public async Task<Result<Option<T>, TError>> BindNoneAsync(Func<Task<Result<Option<T>, TError>>> bind)
        {
            _ = Guard.NotNull(bind, nameof(bind));
            Result<Option<T>, TError> original = result;
            return await result.BindAsync(option => option.IsNone ? bind() : Task.FromResult(original)).ConfigureAwait(false);
        }

        /// <summary>Unwraps Some and turns None into a failure with <paramref name="error"/>; an existing failure is kept.</summary>
        public Result<T, TError> EnsureSome(TError error)
        {
            _ = Guard.NotNull(error, nameof(error));
            return result.Bind(option => option.ToResult(error));
        }

        /// <summary>Maps a found value; a missing value and a failure pass through.</summary>
        public Result<Option<TOut>, TError> MapSome<TOut>(Func<T, TOut> map)
        {
            _ = Guard.NotNull(map, nameof(map));
            return result.Map(option => option.Map(map));
        }

        /// <summary>Asynchronously maps a found value; a missing value and a failure pass through.</summary>
        public async Task<Result<Option<TOut>, TError>> MapSomeAsync<TOut>(Func<T, Task<TOut>> map)
        {
            _ = Guard.NotNull(map, nameof(map));
            return await result.MapAsync(option => option.MapAsync(map)).ConfigureAwait(false);
        }

        /// <summary>Continues with another lookup for a found value; a missing value and a failure pass through.</summary>
        public Result<Option<TOut>, TError> BindSome<TOut>(Func<T, Result<Option<TOut>, TError>> bind)
        {
            _ = Guard.NotNull(bind, nameof(bind));
            return result.Bind(option => option.Match(bind, () => Result<Option<TOut>, TError>.Success(Option<TOut>.None)));
        }

        /// <summary>Asynchronously continues with another lookup for a found value; a missing value and a failure pass through.</summary>
        public async Task<Result<Option<TOut>, TError>> BindSomeAsync<TOut>(Func<T, Task<Result<Option<TOut>, TError>>> bind)
        {
            _ = Guard.NotNull(bind, nameof(bind));
            return await result.BindAsync(option => option.MatchAsync(bind, () => Task.FromResult(Result<Option<TOut>, TError>.Success(Option<TOut>.None)))).ConfigureAwait(false);
        }
    }
}
