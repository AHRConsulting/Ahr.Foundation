namespace Ahr.Foundation;

public static partial class TaskCompositionExtensions
{
    extension<T>(Task<Result<T>> resultTask)
    {
        /// <summary>Awaits and binds a result.</summary>
        public async Task<Result<TOut>> BindAsync<TOut>(Func<T, Task<Result<TOut>>> bind) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).BindAsync(bind).ConfigureAwait(false);

        /// <summary>Awaits and binds to a payload-free result.</summary>
        public async Task<Result> BindAsync(Func<T, Task<Result>> bind) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).BindAsync(bind).ConfigureAwait(false);

        /// <summary>Awaits and maps success.</summary>
        public async Task<Result<TOut>> MapAsync<TOut>(Func<T, Task<TOut>> map) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).MapAsync(map).ConfigureAwait(false);

        /// <summary>Awaits and maps failure.</summary>
        public async Task<Result<T>> MapErrorAsync(Func<Error, Task<Error>> map) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).MapErrorAsync(map).ConfigureAwait(false);

        /// <summary>Awaits and ensures success.</summary>
        public async Task<Result<T>> EnsureAsync(Func<T, Task<bool>> predicate, Error error) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).EnsureAsync(predicate, error).ConfigureAwait(false);

        /// <summary>Awaits and taps success.</summary>
        public async Task<Result<T>> TapAsync(Func<T, Task> action) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).TapAsync(action).ConfigureAwait(false);

        /// <summary>Awaits and taps failure.</summary>
        public async Task<Result<T>> TapErrorAsync(Func<Error, Task> action) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).TapErrorAsync(action).ConfigureAwait(false);

        /// <summary>Awaits and, on failure, invokes and returns the fallback factory.</summary>
        public async Task<Result<T>> OrElseAsync(Func<Task<Result<T>>> fallback) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).OrElseAsync(fallback).ConfigureAwait(false);
    }
}
