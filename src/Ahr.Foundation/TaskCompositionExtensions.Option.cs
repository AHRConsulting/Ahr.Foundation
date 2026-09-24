namespace Ahr.Foundation;

public static partial class TaskCompositionExtensions
{
    extension<T>(Task<Option<T>> optionTask)
    {
        /// <summary>Awaits and binds an option.</summary>
        public async Task<Option<TOut>> BindAsync<TOut>(Func<T, Task<Option<TOut>>> bind) =>
            await (await Guard.NotNull(optionTask, nameof(optionTask)).ConfigureAwait(false)).BindAsync(bind).ConfigureAwait(false);

        /// <summary>Awaits and maps an option.</summary>
        public async Task<Option<TOut>> MapAsync<TOut>(Func<T, Task<TOut>> map) =>
            await (await Guard.NotNull(optionTask, nameof(optionTask)).ConfigureAwait(false)).MapAsync(map).ConfigureAwait(false);

        /// <summary>Awaits and filters an option.</summary>
        public async Task<Option<T>> WhereAsync(Func<T, Task<bool>> predicate) =>
            await (await Guard.NotNull(optionTask, nameof(optionTask)).ConfigureAwait(false)).WhereAsync(predicate).ConfigureAwait(false);

        /// <summary>Awaits and taps an option.</summary>
        public async Task<Option<T>> TapAsync(Func<T, Task> action) =>
            await (await Guard.NotNull(optionTask, nameof(optionTask)).ConfigureAwait(false)).TapAsync(action).ConfigureAwait(false);

        /// <summary>Awaits and, on none, invokes and returns the fallback factory.</summary>
        public async Task<Option<T>> OrElseAsync(Func<Task<Option<T>>> fallback) =>
            await (await Guard.NotNull(optionTask, nameof(optionTask)).ConfigureAwait(false)).OrElseAsync(fallback).ConfigureAwait(false);
    }
}
