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

        /// <summary>Awaits and invokes a step on success, keeping the value; a failed step becomes the failure.</summary>
        public async Task<Result<T, TError>> TapAsync<TOut>(Func<T, Task<Result<TOut, TError>>> action) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).TapAsync(action).ConfigureAwait(false);

        /// <summary>Awaits and invokes a synchronous step on success, keeping the value; a failed step becomes the failure.</summary>
        public async Task<Result<T, TError>> TapAsync<TOut>(Func<T, Result<TOut, TError>> action) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).Tap(action);

        /// <summary>Awaits and taps custom-error success.</summary>
        public async Task<Result<T, TError>> TapAsync(Func<T, Task> action) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).TapAsync(action).ConfigureAwait(false);

        /// <summary>Awaits and taps custom failure.</summary>
        public async Task<Result<T, TError>> TapErrorAsync(Func<TError, Task> action) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).TapErrorAsync(action).ConfigureAwait(false);

        /// <summary>Awaits and, on failure, invokes and returns the fallback factory.</summary>
        public async Task<Result<T, TError>> OrElseAsync(Func<Task<Result<T, TError>>> fallback) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).OrElseAsync(fallback).ConfigureAwait(false);

        /// <summary>Awaits and binds a custom-error result with a synchronous function.</summary>
        public async Task<Result<TOut, TError>> BindAsync<TOut>(Func<T, Result<TOut, TError>> bind) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).Bind(bind);

        /// <summary>Awaits and maps custom-error success with a synchronous function.</summary>
        public async Task<Result<TOut, TError>> MapAsync<TOut>(Func<T, TOut> map) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).Map(map);

        /// <summary>Awaits and maps custom failure with a synchronous function.</summary>
        public async Task<Result<T, TOutError>> MapErrorAsync<TOutError>(Func<TError, TOutError> map) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).MapError(map);

        /// <summary>Awaits and ensures custom-error success with a synchronous predicate.</summary>
        public async Task<Result<T, TError>> EnsureAsync(Func<T, bool> predicate, TError error) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).Ensure(predicate, error);

        /// <summary>Awaits and invokes a synchronous action on custom-error success.</summary>
        public async Task<Result<T, TError>> TapAsync(Action<T> action) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).Tap(action);

        /// <summary>Awaits and invokes a synchronous action on custom failure.</summary>
        public async Task<Result<T, TError>> TapErrorAsync(Action<TError> action) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).TapError(action);

        /// <summary>Awaits and matches the active branch with synchronous functions.</summary>
        public async Task<TResult> MatchAsync<TResult>(Func<T, TResult> onSuccess, Func<TError, TResult> onFailure) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).Match(onSuccess, onFailure);

        /// <summary>Awaits and asynchronously matches the active branch.</summary>
        public async Task<TResult> MatchAsync<TResult>(Func<T, Task<TResult>> onSuccess, Func<TError, Task<TResult>> onFailure) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).MatchAsync(onSuccess, onFailure).ConfigureAwait(false);

        /// <summary>Awaits the task and returns a failure with <paramref name="error"/> when <paramref name="cancellationToken"/> canceled it; any other cancellation or exception propagates unchanged.</summary>
        public async Task<Result<T, TError>> CatchCancellationAsync(TError error, CancellationToken cancellationToken)
        {
            _ = Guard.NotNull(resultTask, nameof(resultTask));
            _ = Guard.NotNull(error, nameof(error));
            try
            {
                return await resultTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Result<T, TError>.Failure(error);
            }
        }

        /// <summary>Awaits the task and returns a failure built by <paramref name="errorFactory"/> when <paramref name="cancellationToken"/> canceled it; any other cancellation or exception propagates unchanged.</summary>
        public async Task<Result<T, TError>> CatchCancellationAsync(Func<OperationCanceledException, TError> errorFactory, CancellationToken cancellationToken)
        {
            _ = Guard.NotNull(resultTask, nameof(resultTask));
            _ = Guard.NotNull(errorFactory, nameof(errorFactory));
            try
            {
                return await resultTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
            {
                return Result<T, TError>.Failure(errorFactory(exception));
            }
        }
    }
}
