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

        /// <summary>Awaits and invokes a step on success; a failed step becomes the failure.</summary>
        public async Task<Result> TapAsync(Func<Task<Result>> action) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).TapAsync(action).ConfigureAwait(false);

        /// <summary>Awaits and invokes a step on success; a failed step becomes the failure.</summary>
        public async Task<Result> TapAsync<TOut>(Func<Task<Result<TOut>>> action) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).TapAsync(action).ConfigureAwait(false);

        /// <summary>Awaits and invokes a synchronous step on success; a failed step becomes the failure.</summary>
        public async Task<Result> TapAsync(Func<Result> action) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).Tap(action);

        /// <summary>Awaits and invokes a synchronous step on success; a failed step becomes the failure.</summary>
        public async Task<Result> TapAsync<TOut>(Func<Result<TOut>> action) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).Tap(action);

        /// <summary>Awaits and taps success.</summary>
        public async Task<Result> TapAsync(Func<Task> action) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).TapAsync(action).ConfigureAwait(false);

        /// <summary>Awaits and taps failure.</summary>
        public async Task<Result> TapErrorAsync(Func<Error, Task> action) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).TapErrorAsync(action).ConfigureAwait(false);

        /// <summary>Awaits and asynchronously maps a failure error.</summary>
        public async Task<Result> MapErrorAsync(Func<Error, Task<Error>> map) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).MapErrorAsync(map).ConfigureAwait(false);

        /// <summary>Awaits and maps a failure error with a synchronous function.</summary>
        public async Task<Result> MapErrorAsync(Func<Error, Error> map) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).MapError(map);

        /// <summary>Awaits and, on failure, invokes and returns the fallback factory.</summary>
        public async Task<Result> OrElseAsync(Func<Task<Result>> fallback) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).OrElseAsync(fallback).ConfigureAwait(false);

        /// <summary>Awaits and binds a result with a synchronous function.</summary>
        public async Task<Result> BindAsync(Func<Result> bind) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).Bind(bind);

        /// <summary>Awaits and binds a result to a value result with a synchronous function.</summary>
        public async Task<Result<TOut>> BindAsync<TOut>(Func<Result<TOut>> bind) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).Bind(bind);

        /// <summary>Awaits and invokes a synchronous action on success.</summary>
        public async Task<Result> TapAsync(Action action) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).Tap(action);

        /// <summary>Awaits and invokes a synchronous action on failure.</summary>
        public async Task<Result> TapErrorAsync(Action<Error> action) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).TapError(action);

        /// <summary>Awaits and matches the active branch with synchronous functions.</summary>
        public async Task<TResult> MatchAsync<TResult>(Func<TResult> onSuccess, Func<Error, TResult> onFailure) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).Match(onSuccess, onFailure);

        /// <summary>Awaits and asynchronously matches the active branch.</summary>
        public async Task<TResult> MatchAsync<TResult>(Func<Task<TResult>> onSuccess, Func<Error, Task<TResult>> onFailure) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).MatchAsync(onSuccess, onFailure).ConfigureAwait(false);

        /// <summary>Awaits the task and returns a failure with <paramref name="error"/> when <paramref name="cancellationToken"/> canceled it; any other cancellation or exception propagates unchanged.</summary>
        public async Task<Result> CatchCancellationAsync(Error error, CancellationToken cancellationToken)
        {
            _ = Guard.NotNull(resultTask, nameof(resultTask));
            _ = Guard.ValidError(error, nameof(error));
            try
            {
                return await resultTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Result.Failure(error);
            }
        }

        /// <summary>Awaits the task and returns a failure built by <paramref name="errorFactory"/> when <paramref name="cancellationToken"/> canceled it; any other cancellation or exception propagates unchanged.</summary>
        public async Task<Result> CatchCancellationAsync(Func<OperationCanceledException, Error> errorFactory, CancellationToken cancellationToken)
        {
            _ = Guard.NotNull(resultTask, nameof(resultTask));
            _ = Guard.NotNull(errorFactory, nameof(errorFactory));
            try
            {
                return await resultTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
            {
                return Result.Failure(errorFactory(exception));
            }
        }
    }
}
