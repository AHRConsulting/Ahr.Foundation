namespace Ahr.Foundation;

public static partial class TaskCompositionExtensions
{
    extension<T>(Task<Option<T>> optionTask)
    {
        /// <summary>Awaits and converts Some to success and None to failure.</summary>
        public async Task<Result<T>> ToResultAsync(Error error) =>
            (await Guard.NotNull(optionTask, nameof(optionTask)).ConfigureAwait(false)).ToResult(error);

        /// <summary>Awaits and converts Some to success and None to a custom failure.</summary>
        public async Task<Result<T, TError>> ToResultAsync<TError>(TError error) =>
            (await Guard.NotNull(optionTask, nameof(optionTask)).ConfigureAwait(false)).ToResult(error);

        /// <summary>Awaits and binds an option.</summary>
        public async Task<Option<TOut>> BindAsync<TOut>(Func<T, Task<Option<TOut>>> bind) =>
            await (await Guard.NotNull(optionTask, nameof(optionTask)).ConfigureAwait(false)).BindAsync(bind).ConfigureAwait(false);

        /// <summary>Awaits and maps an option.</summary>
        public async Task<Option<TOut>> MapAsync<TOut>(Func<T, Task<TOut>> map) =>
            await (await Guard.NotNull(optionTask, nameof(optionTask)).ConfigureAwait(false)).MapAsync(map).ConfigureAwait(false);

        /// <summary>Awaits and filters an option.</summary>
        public async Task<Option<T>> WhereAsync(Func<T, Task<bool>> predicate) =>
            await (await Guard.NotNull(optionTask, nameof(optionTask)).ConfigureAwait(false)).WhereAsync(predicate).ConfigureAwait(false);

        /// <summary>Awaits and invokes a step for a present value, keeping it; a step returning None makes the result None.</summary>
        public async Task<Option<T>> TapAsync<TOut>(Func<T, Task<Option<TOut>>> action) =>
            await (await Guard.NotNull(optionTask, nameof(optionTask)).ConfigureAwait(false)).TapAsync(action).ConfigureAwait(false);

        /// <summary>Awaits and invokes a synchronous step for a present value, keeping it; a step returning None makes the result None.</summary>
        public async Task<Option<T>> TapAsync<TOut>(Func<T, Option<TOut>> action) =>
            (await Guard.NotNull(optionTask, nameof(optionTask)).ConfigureAwait(false)).Tap(action);

        /// <summary>Awaits and taps an option.</summary>
        public async Task<Option<T>> TapAsync(Func<T, Task> action) =>
            await (await Guard.NotNull(optionTask, nameof(optionTask)).ConfigureAwait(false)).TapAsync(action).ConfigureAwait(false);

        /// <summary>Awaits and, on none, invokes and returns the fallback factory.</summary>
        public async Task<Option<T>> OrElseAsync(Func<Task<Option<T>>> fallback) =>
            await (await Guard.NotNull(optionTask, nameof(optionTask)).ConfigureAwait(false)).OrElseAsync(fallback).ConfigureAwait(false);

        /// <summary>Awaits and binds an option with a synchronous function.</summary>
        public async Task<Option<TOut>> BindAsync<TOut>(Func<T, Option<TOut>> bind) =>
            (await Guard.NotNull(optionTask, nameof(optionTask)).ConfigureAwait(false)).Bind(bind);

        /// <summary>Awaits and maps an option with a synchronous function.</summary>
        public async Task<Option<TOut>> MapAsync<TOut>(Func<T, TOut> map) =>
            (await Guard.NotNull(optionTask, nameof(optionTask)).ConfigureAwait(false)).Map(map);

        /// <summary>Awaits and filters an option with a synchronous predicate.</summary>
        public async Task<Option<T>> WhereAsync(Func<T, bool> predicate) =>
            (await Guard.NotNull(optionTask, nameof(optionTask)).ConfigureAwait(false)).Where(predicate);

        /// <summary>Awaits and invokes a synchronous action for a present value.</summary>
        public async Task<Option<T>> TapAsync(Action<T> action) =>
            (await Guard.NotNull(optionTask, nameof(optionTask)).ConfigureAwait(false)).Tap(action);

        /// <summary>Awaits and matches the active branch with synchronous functions.</summary>
        public async Task<TResult> MatchAsync<TResult>(Func<T, TResult> onSome, Func<TResult> onNone) =>
            (await Guard.NotNull(optionTask, nameof(optionTask)).ConfigureAwait(false)).Match(onSome, onNone);

        /// <summary>Awaits and asynchronously matches the active branch.</summary>
        public async Task<TResult> MatchAsync<TResult>(Func<T, Task<TResult>> onSome, Func<Task<TResult>> onNone) =>
            await (await Guard.NotNull(optionTask, nameof(optionTask)).ConfigureAwait(false)).MatchAsync(onSome, onNone).ConfigureAwait(false);
    }
}
