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

        /// <summary>Awaits and maps failure to a custom error type.</summary>
        public async Task<Result<T, TOutError>> MapErrorAsync<TOutError>(Func<Error, Task<TOutError>> map) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).MapErrorAsync(map).ConfigureAwait(false);

        /// <summary>Awaits and maps failure to a custom error type with a synchronous function.</summary>
        public async Task<Result<T, TOutError>> MapErrorAsync<TOutError>(Func<Error, TOutError> map) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).MapError(map);

        /// <summary>Awaits and drops the success value, keeping only whether the operation succeeded.</summary>
        public async Task<Result> ToResultAsync() =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).ToResult();

        /// <summary>Awaits and ensures success.</summary>
        public async Task<Result<T>> EnsureAsync(Func<T, Task<bool>> predicate, Error error) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).EnsureAsync(predicate, error).ConfigureAwait(false);

        /// <summary>Awaits and ensures success, failing with an error created from the message.</summary>
        public async Task<Result<T>> EnsureAsync(Func<T, Task<bool>> predicate, string message) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).EnsureAsync(predicate, message).ConfigureAwait(false);

        /// <summary>Awaits and ensures success with a synchronous predicate, failing with an error created from the message.</summary>
        public async Task<Result<T>> EnsureAsync(Func<T, bool> predicate, string message) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).Ensure(predicate, message);

        /// <summary>Awaits and invokes a step on success, keeping the value; a failed step becomes the failure.</summary>
        public async Task<Result<T>> TapAsync(Func<T, Task<Result>> action) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).TapAsync(action).ConfigureAwait(false);

        /// <summary>Awaits and invokes a step on success, keeping the value; a failed step becomes the failure.</summary>
        public async Task<Result<T>> TapAsync<TOut>(Func<T, Task<Result<TOut>>> action) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).TapAsync(action).ConfigureAwait(false);

        /// <summary>Awaits and invokes a synchronous step on success, keeping the value; a failed step becomes the failure.</summary>
        public async Task<Result<T>> TapAsync(Func<T, Result> action) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).Tap(action);

        /// <summary>Awaits and invokes a synchronous step on success, keeping the value; a failed step becomes the failure.</summary>
        public async Task<Result<T>> TapAsync<TOut>(Func<T, Result<TOut>> action) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).Tap(action);

        /// <summary>Awaits and taps success.</summary>
        public async Task<Result<T>> TapAsync(Func<T, Task> action) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).TapAsync(action).ConfigureAwait(false);

        /// <summary>Awaits and taps failure.</summary>
        public async Task<Result<T>> TapErrorAsync(Func<Error, Task> action) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).TapErrorAsync(action).ConfigureAwait(false);

        /// <summary>Awaits and, on failure, invokes and returns the fallback factory.</summary>
        public async Task<Result<T>> OrElseAsync(Func<Task<Result<T>>> fallback) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).OrElseAsync(fallback).ConfigureAwait(false);

        /// <summary>Awaits and binds a result with a synchronous function.</summary>
        public async Task<Result<TOut>> BindAsync<TOut>(Func<T, Result<TOut>> bind) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).Bind(bind);

        /// <summary>Awaits and binds to a payload-free result with a synchronous function.</summary>
        public async Task<Result> BindAsync(Func<T, Result> bind) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).Bind(bind);

        /// <summary>Awaits and maps success with a synchronous function.</summary>
        public async Task<Result<TOut>> MapAsync<TOut>(Func<T, TOut> map) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).Map(map);

        /// <summary>Awaits and maps failure with a synchronous function.</summary>
        public async Task<Result<T>> MapErrorAsync(Func<Error, Error> map) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).MapError(map);

        /// <summary>Awaits and ensures success with a synchronous predicate.</summary>
        public async Task<Result<T>> EnsureAsync(Func<T, bool> predicate, Error error) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).Ensure(predicate, error);

        /// <summary>Awaits and invokes a synchronous action on success.</summary>
        public async Task<Result<T>> TapAsync(Action<T> action) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).Tap(action);

        /// <summary>Awaits and invokes a synchronous action on failure.</summary>
        public async Task<Result<T>> TapErrorAsync(Action<Error> action) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).TapError(action);

        /// <summary>Awaits and matches the active branch with synchronous functions.</summary>
        public async Task<TResult> MatchAsync<TResult>(Func<T, TResult> onSuccess, Func<Error, TResult> onFailure) =>
            (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).Match(onSuccess, onFailure);

        /// <summary>Awaits and asynchronously matches the active branch.</summary>
        public async Task<TResult> MatchAsync<TResult>(Func<T, Task<TResult>> onSuccess, Func<Error, Task<TResult>> onFailure) =>
            await (await Guard.NotNull(resultTask, nameof(resultTask)).ConfigureAwait(false)).MatchAsync(onSuccess, onFailure).ConfigureAwait(false);

        /// <summary>Awaits the task and returns a failure with <paramref name="error"/> when <paramref name="cancellationToken"/> canceled it; any other cancellation or exception propagates unchanged.</summary>
        public async Task<Result<T>> CatchCancellationAsync(Error error, CancellationToken cancellationToken)
        {
            _ = Guard.NotNull(resultTask, nameof(resultTask));
            _ = Guard.ValidError(error, nameof(error));
            try
            {
                return await resultTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Result<T>.Failure(error);
            }
        }

        /// <summary>Awaits the task and returns a failure built by <paramref name="errorFactory"/> when <paramref name="cancellationToken"/> canceled it; any other cancellation or exception propagates unchanged.</summary>
        public async Task<Result<T>> CatchCancellationAsync(Func<OperationCanceledException, Error> errorFactory, CancellationToken cancellationToken)
        {
            _ = Guard.NotNull(resultTask, nameof(resultTask));
            _ = Guard.NotNull(errorFactory, nameof(errorFactory));
            try
            {
                return await resultTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
            {
                return Result<T>.Failure(errorFactory(exception));
            }
        }
    }
}
