namespace Ahr.Foundation;

public static partial class TaskCompositionExtensions
{
    extension<T, TError>(Task<Result<T, TError>> resultTask)
    {
        /// <summary>Awaits and binds a custom-error result.</summary>
        public async Task<Result<TOut, TError>> BindAsync<TOut>(Func<T, Task<Result<TOut, TError>>> bind) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).BindAsync(bind).ConfigureAwait(false);

        /// <summary>Awaits and maps custom-error success.</summary>
        public async Task<Result<TOut, TError>> MapAsync<TOut>(Func<T, Task<TOut>> map) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).MapAsync(map).ConfigureAwait(false);

        /// <summary>Awaits and maps custom failure.</summary>
        public async Task<Result<T, TOutError>> MapErrorAsync<TOutError>(Func<TError, Task<TOutError>> map) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).MapErrorAsync(map).ConfigureAwait(false);

        /// <summary>Awaits and ensures custom-error success.</summary>
        public async Task<Result<T, TError>> EnsureAsync(Func<T, Task<bool>> predicate, TError error) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).EnsureAsync(predicate, error).ConfigureAwait(false);

        /// <summary>Awaits and taps custom-error success.</summary>
        public async Task<Result<T, TError>> TapAsync(Func<T, Task> action) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).TapAsync(action).ConfigureAwait(false);

        /// <summary>Awaits and taps custom failure.</summary>
        public async Task<Result<T, TError>> TapErrorAsync(Func<TError, Task> action) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).TapErrorAsync(action).ConfigureAwait(false);

        /// <summary>Awaits and, on failure, invokes and returns the fallback factory.</summary>
        public async Task<Result<T, TError>> OrElseAsync(Func<Task<Result<T, TError>>> fallback) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).OrElseAsync(fallback).ConfigureAwait(false);
    }
}
