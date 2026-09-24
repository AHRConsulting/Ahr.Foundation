namespace Ahr.Foundation;

/// <summary>Task-based composition over results and options.</summary>
public static partial class TaskCompositionExtensions
{
    extension(Task<Result> resultTask)
    {
        /// <summary>Awaits and binds a result.</summary>
        public async Task<Result> BindAsync(Func<Task<Result>> bind) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).BindAsync(bind).ConfigureAwait(false);

        /// <summary>Awaits and binds a result to a value result.</summary>
        public async Task<Result<TOut>> BindAsync<TOut>(Func<Task<Result<TOut>>> bind) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).BindAsync(bind).ConfigureAwait(false);

        /// <summary>Awaits and taps success.</summary>
        public async Task<Result> TapAsync(Func<Task> action) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).TapAsync(action).ConfigureAwait(false);

        /// <summary>Awaits and taps failure.</summary>
        public async Task<Result> TapErrorAsync(Func<Error, Task> action) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).TapErrorAsync(action).ConfigureAwait(false);

        /// <summary>Awaits and, on failure, invokes and returns the fallback factory.</summary>
        public async Task<Result> OrElseAsync(Func<Task<Result>> fallback) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).OrElseAsync(fallback).ConfigureAwait(false);
    }
}
