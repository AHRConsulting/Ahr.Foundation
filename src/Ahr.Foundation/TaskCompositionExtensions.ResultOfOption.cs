namespace Ahr.Foundation;

public static partial class TaskCompositionExtensions
{
    extension<T>(Task<Result<Option<T>>> resultTask)
    {
        /// <summary>Awaits and matches a found value, a missing value, or a failure with synchronous functions.</summary>
        public async Task<TResult> MatchAsync<TResult>(Func<T, TResult> onSome, Func<TResult> onNone, Func<Error, TResult> onFailure) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).Match(onSome, onNone, onFailure);

        /// <summary>Awaits and asynchronously matches a found value, a missing value, or a failure.</summary>
        public async Task<TResult> MatchAsync<TResult>(Func<T, Task<TResult>> onSome, Func<Task<TResult>> onNone, Func<Error, Task<TResult>> onFailure) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).MatchAsync(onSome, onNone, onFailure).ConfigureAwait(false);

        /// <summary>Awaits and runs the action for a found value, a missing value, or a failure, and returns the original result.</summary>
        public async Task<Result<Option<T>>> MatchAsync(Action<T> onSome, Action onNone, Action<Error> onFailure) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).Match(onSome, onNone, onFailure);

        /// <summary>Awaits and asynchronously runs the action for a found value, a missing value, or a failure, and returns the original result.</summary>
        public async Task<Result<Option<T>>> MatchAsync(Func<T, Task> onSome, Func<Task> onNone, Func<Error, Task> onFailure) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).MatchAsync(onSome, onNone, onFailure).ConfigureAwait(false);

        /// <summary>Awaits and runs a synchronous action for a found value; a missing value and a failure pass through.</summary>
        public async Task<Result<Option<T>>> TapSomeAsync(Action<T> action) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).TapSome(action);

        /// <summary>Awaits and asynchronously runs an action for a found value; a missing value and a failure pass through.</summary>
        public async Task<Result<Option<T>>> TapSomeAsync(Func<T, Task> action) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).TapSomeAsync(action).ConfigureAwait(false);

        /// <summary>Awaits and runs <paramref name="action"/> for a found value; a failure from <paramref name="action"/> stops the pipeline.</summary>
        public async Task<Result<Option<T>>> TapSomeAsync(Func<T, Result> action) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).TapSome(action);

        /// <summary>Awaits and runs <paramref name="action"/> for a found value; a failure from <paramref name="action"/> stops the pipeline.</summary>
        public async Task<Result<Option<T>>> TapSomeAsync<TOut>(Func<T, Result<TOut>> action) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).TapSome(action);

        /// <summary>Awaits and asynchronously runs <paramref name="action"/> for a found value; a failure from <paramref name="action"/> stops the pipeline.</summary>
        public async Task<Result<Option<T>>> TapSomeAsync(Func<T, Task<Result>> action) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).TapSomeAsync(action).ConfigureAwait(false);

        /// <summary>Awaits and asynchronously runs <paramref name="action"/> for a found value; a failure from <paramref name="action"/> stops the pipeline.</summary>
        public async Task<Result<Option<T>>> TapSomeAsync<TOut>(Func<T, Task<Result<TOut>>> action) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).TapSomeAsync(action).ConfigureAwait(false);

        /// <summary>Awaits, unwraps Some, and turns None into <paramref name="fallback"/>; an existing failure is kept.</summary>
        public async Task<Result<T>> SomeOrAsync(T fallback) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).SomeOr(fallback);

        /// <summary>Awaits and continues with a synchronous lookup when no value was found; a found value and a failure pass through.</summary>
        public async Task<Result<Option<T>>> BindNoneAsync(Func<Result<Option<T>>> bind) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).BindNone(bind);

        /// <summary>Awaits and continues with another lookup when no value was found; a found value and a failure pass through.</summary>
        public async Task<Result<Option<T>>> BindNoneAsync(Func<Task<Result<Option<T>>>> bind) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).BindNoneAsync(bind).ConfigureAwait(false);

        /// <summary>Awaits, unwraps Some, and turns None into a failure with <paramref name="error"/>; an existing failure is kept.</summary>
        public async Task<Result<T>> EnsureSomeAsync(Error error) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).EnsureSome(error);

        /// <summary>Awaits and maps a found value with a synchronous function; a missing value and a failure pass through.</summary>
        public async Task<Result<Option<TOut>>> MapSomeAsync<TOut>(Func<T, TOut> map) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).MapSome(map);

        /// <summary>Awaits and asynchronously maps a found value; a missing value and a failure pass through.</summary>
        public async Task<Result<Option<TOut>>> MapSomeAsync<TOut>(Func<T, Task<TOut>> map) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).MapSomeAsync(map).ConfigureAwait(false);

        /// <summary>Awaits and continues with a synchronous lookup for a found value; a missing value and a failure pass through.</summary>
        public async Task<Result<Option<TOut>>> BindSomeAsync<TOut>(Func<T, Result<Option<TOut>>> bind) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).BindSome(bind);

        /// <summary>Awaits and continues with another lookup for a found value; a missing value and a failure pass through.</summary>
        public async Task<Result<Option<TOut>>> BindSomeAsync<TOut>(Func<T, Task<Result<Option<TOut>>>> bind) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).BindSomeAsync(bind).ConfigureAwait(false);
    }

    extension<T, TError>(Task<Result<Option<T>, TError>> resultTask)
    {
        /// <summary>Awaits and matches a found value, a missing value, or a failure with synchronous functions.</summary>
        public async Task<TResult> MatchAsync<TResult>(Func<T, TResult> onSome, Func<TResult> onNone, Func<TError, TResult> onFailure) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).Match(onSome, onNone, onFailure);

        /// <summary>Awaits and asynchronously matches a found value, a missing value, or a failure.</summary>
        public async Task<TResult> MatchAsync<TResult>(Func<T, Task<TResult>> onSome, Func<Task<TResult>> onNone, Func<TError, Task<TResult>> onFailure) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).MatchAsync(onSome, onNone, onFailure).ConfigureAwait(false);

        /// <summary>Awaits and runs the action for a found value, a missing value, or a failure, and returns the original result.</summary>
        public async Task<Result<Option<T>, TError>> MatchAsync(Action<T> onSome, Action onNone, Action<TError> onFailure) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).Match(onSome, onNone, onFailure);

        /// <summary>Awaits and asynchronously runs the action for a found value, a missing value, or a failure, and returns the original result.</summary>
        public async Task<Result<Option<T>, TError>> MatchAsync(Func<T, Task> onSome, Func<Task> onNone, Func<TError, Task> onFailure) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).MatchAsync(onSome, onNone, onFailure).ConfigureAwait(false);

        /// <summary>Awaits and runs a synchronous action for a found value; a missing value and a failure pass through.</summary>
        public async Task<Result<Option<T>, TError>> TapSomeAsync(Action<T> action) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).TapSome(action);

        /// <summary>Awaits and asynchronously runs an action for a found value; a missing value and a failure pass through.</summary>
        public async Task<Result<Option<T>, TError>> TapSomeAsync(Func<T, Task> action) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).TapSomeAsync(action).ConfigureAwait(false);

        /// <summary>Awaits and runs <paramref name="action"/> for a found value; a failure from <paramref name="action"/> stops the pipeline.</summary>
        public async Task<Result<Option<T>, TError>> TapSomeAsync<TOut>(Func<T, Result<TOut, TError>> action) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).TapSome(action);

        /// <summary>Awaits and asynchronously runs <paramref name="action"/> for a found value; a failure from <paramref name="action"/> stops the pipeline.</summary>
        public async Task<Result<Option<T>, TError>> TapSomeAsync<TOut>(Func<T, Task<Result<TOut, TError>>> action) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).TapSomeAsync(action).ConfigureAwait(false);

        /// <summary>Awaits, unwraps Some, and turns None into <paramref name="fallback"/>; an existing failure is kept.</summary>
        public async Task<Result<T, TError>> SomeOrAsync(T fallback) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).SomeOr(fallback);

        /// <summary>Awaits and continues with a synchronous lookup when no value was found; a found value and a failure pass through.</summary>
        public async Task<Result<Option<T>, TError>> BindNoneAsync(Func<Result<Option<T>, TError>> bind) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).BindNone(bind);

        /// <summary>Awaits and continues with another lookup when no value was found; a found value and a failure pass through.</summary>
        public async Task<Result<Option<T>, TError>> BindNoneAsync(Func<Task<Result<Option<T>, TError>>> bind) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).BindNoneAsync(bind).ConfigureAwait(false);

        /// <summary>Awaits, unwraps Some, and turns None into a failure with <paramref name="error"/>; an existing failure is kept.</summary>
        public async Task<Result<T, TError>> EnsureSomeAsync(TError error) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).EnsureSome(error);

        /// <summary>Awaits and maps a found value with a synchronous function; a missing value and a failure pass through.</summary>
        public async Task<Result<Option<TOut>, TError>> MapSomeAsync<TOut>(Func<T, TOut> map) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).MapSome(map);

        /// <summary>Awaits and asynchronously maps a found value; a missing value and a failure pass through.</summary>
        public async Task<Result<Option<TOut>, TError>> MapSomeAsync<TOut>(Func<T, Task<TOut>> map) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).MapSomeAsync(map).ConfigureAwait(false);

        /// <summary>Awaits and continues with a synchronous lookup for a found value; a missing value and a failure pass through.</summary>
        public async Task<Result<Option<TOut>, TError>> BindSomeAsync<TOut>(Func<T, Result<Option<TOut>, TError>> bind) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).BindSome(bind);

        /// <summary>Awaits and continues with another lookup for a found value; a missing value and a failure pass through.</summary>
        public async Task<Result<Option<TOut>, TError>> BindSomeAsync<TOut>(Func<T, Task<Result<Option<TOut>, TError>>> bind) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).BindSomeAsync(bind).ConfigureAwait(false);
    }
}
