namespace Ahr.Foundation;

public static partial class ResultExtensions
{
    extension<T>(IEnumerable<T> source)
    {
        /// <summary>Runs <paramref name="step"/> on each item in order and stops at the first failure.</summary>
        public Result Traverse(Func<T, Result> step)
        {
            _ = Guard.NotNull(source, nameof(source));
            _ = Guard.NotNull(step, nameof(step));
            return source.Select(step).Sequence();
        }

        /// <summary>Asynchronously runs <paramref name="step"/> on each item, one at a time, and stops at the first failure.</summary>
        public async Task<Result> TraverseAsync(Func<T, Task<Result>> step)
        {
            _ = Guard.NotNull(source, nameof(source));
            _ = Guard.NotNull(step, nameof(step));
            return await source.Select(step).SequenceAsync().ConfigureAwait(false);
        }

        /// <summary>Runs <paramref name="step"/> on each item in order and returns all the values, or the first failure.</summary>
        public Result<IReadOnlyList<TOut>> Traverse<TOut>(Func<T, Result<TOut>> step)
        {
            _ = Guard.NotNull(source, nameof(source));
            _ = Guard.NotNull(step, nameof(step));
            return source.Select(step).Sequence();
        }

        /// <summary>Asynchronously runs <paramref name="step"/> on each item, one at a time, and returns all the values, or the first failure.</summary>
        public async Task<Result<IReadOnlyList<TOut>>> TraverseAsync<TOut>(Func<T, Task<Result<TOut>>> step)
        {
            _ = Guard.NotNull(source, nameof(source));
            _ = Guard.NotNull(step, nameof(step));
            return await source.Select(step).SequenceAsync().ConfigureAwait(false);
        }

        /// <summary>Runs <paramref name="step"/> on each item in order and returns all the values, or the first failure.</summary>
        public Result<IReadOnlyList<TOut>, TError> Traverse<TOut, TError>(Func<T, Result<TOut, TError>> step)
        {
            _ = Guard.NotNull(source, nameof(source));
            _ = Guard.NotNull(step, nameof(step));
            return source.Select(step).Sequence();
        }

        /// <summary>Asynchronously runs <paramref name="step"/> on each item, one at a time, and returns all the values, or the first failure.</summary>
        public async Task<Result<IReadOnlyList<TOut>, TError>> TraverseAsync<TOut, TError>(Func<T, Task<Result<TOut, TError>>> step)
        {
            _ = Guard.NotNull(source, nameof(source));
            _ = Guard.NotNull(step, nameof(step));
            return await source.Select(step).SequenceAsync().ConfigureAwait(false);
        }

        /// <summary>Runs <paramref name="step"/> on every item and returns every error; an empty list means every step succeeded.</summary>
        public IReadOnlyList<Error> Partition(Func<T, Result> step)
        {
            _ = Guard.NotNull(source, nameof(source));
            _ = Guard.NotNull(step, nameof(step));
            return source.Select(step).Partition();
        }

        /// <summary>Asynchronously runs <paramref name="step"/> on every item, one at a time, and returns every error; an empty list means every step succeeded.</summary>
        public async Task<IReadOnlyList<Error>> PartitionAsync(Func<T, Task<Result>> step)
        {
            _ = Guard.NotNull(source, nameof(source));
            _ = Guard.NotNull(step, nameof(step));
            return await source.Select(step).PartitionAsync().ConfigureAwait(false);
        }

        /// <summary>Runs <paramref name="step"/> on every item and returns the values and the errors separately.</summary>
        public (IReadOnlyList<TOut> Values, IReadOnlyList<Error> Errors) Partition<TOut>(Func<T, Result<TOut>> step)
        {
            _ = Guard.NotNull(source, nameof(source));
            _ = Guard.NotNull(step, nameof(step));
            return source.Select(step).Partition();
        }

        /// <summary>Asynchronously runs <paramref name="step"/> on every item, one at a time, and returns the values and the errors separately.</summary>
        public async Task<(IReadOnlyList<TOut> Values, IReadOnlyList<Error> Errors)> PartitionAsync<TOut>(Func<T, Task<Result<TOut>>> step)
        {
            _ = Guard.NotNull(source, nameof(source));
            _ = Guard.NotNull(step, nameof(step));
            return await source.Select(step).PartitionAsync().ConfigureAwait(false);
        }

        /// <summary>Runs <paramref name="step"/> on every item and returns the values and the errors separately.</summary>
        public (IReadOnlyList<TOut> Values, IReadOnlyList<TError> Errors) Partition<TOut, TError>(Func<T, Result<TOut, TError>> step)
        {
            _ = Guard.NotNull(source, nameof(source));
            _ = Guard.NotNull(step, nameof(step));
            return source.Select(step).Partition();
        }

        /// <summary>Asynchronously runs <paramref name="step"/> on every item, one at a time, and returns the values and the errors separately.</summary>
        public async Task<(IReadOnlyList<TOut> Values, IReadOnlyList<TError> Errors)> PartitionAsync<TOut, TError>(Func<T, Task<Result<TOut, TError>>> step)
        {
            _ = Guard.NotNull(source, nameof(source));
            _ = Guard.NotNull(step, nameof(step));
            return await source.Select(step).PartitionAsync().ConfigureAwait(false);
        }
    }

    extension(IEnumerable<Result> results)
    {
        /// <summary>Returns success when every result succeeded, or the first failure; results after it are not read.</summary>
        public Result Sequence()
        {
            foreach (Result result in Guard.NotNull(results, nameof(results)))
            {
                if (result.IsFailure)
                {
                    return result;
                }
            }

            return Result.Success();
        }

        /// <summary>Returns every error; an empty list means every result succeeded.</summary>
        public IReadOnlyList<Error> Partition()
        {
            List<Error> errors = [];
            foreach (Result result in Guard.NotNull(results, nameof(results)))
            {
                if (result.IsFailure)
                {
                    errors.Add(result.Error);
                }
            }

            return errors;
        }
    }

    extension<T>(IEnumerable<Result<T>> results)
    {
        /// <summary>Returns all the values when every result succeeded, or the first failure; results after it are not read.</summary>
        public Result<IReadOnlyList<T>> Sequence()
        {
            List<T> values = [];
            foreach (Result<T> result in Guard.NotNull(results, nameof(results)))
            {
                if (result.IsFailure)
                {
                    return Result<IReadOnlyList<T>>.Failure(result.Error);
                }

                values.Add(result.Value);
            }

            return Result<IReadOnlyList<T>>.Success(values);
        }

        /// <summary>Returns the values and the errors separately.</summary>
        public (IReadOnlyList<T> Values, IReadOnlyList<Error> Errors) Partition()
        {
            List<T> values = [];
            List<Error> errors = [];
            foreach (Result<T> result in Guard.NotNull(results, nameof(results)))
            {
                if (result.IsSuccess)
                {
                    values.Add(result.Value);
                }
                else
                {
                    errors.Add(result.Error);
                }
            }

            return (values, errors);
        }
    }

    extension<T, TError>(IEnumerable<Result<T, TError>> results)
    {
        /// <summary>Returns all the values when every result succeeded, or the first failure; results after it are not read.</summary>
        public Result<IReadOnlyList<T>, TError> Sequence()
        {
            List<T> values = [];
            foreach (Result<T, TError> result in Guard.NotNull(results, nameof(results)))
            {
                if (result.IsFailure)
                {
                    return Result<IReadOnlyList<T>, TError>.Failure(result.Error);
                }

                values.Add(result.Value);
            }

            return Result<IReadOnlyList<T>, TError>.Success(values);
        }

        /// <summary>Returns the values and the errors separately.</summary>
        public (IReadOnlyList<T> Values, IReadOnlyList<TError> Errors) Partition()
        {
            List<T> values = [];
            List<TError> errors = [];
            foreach (Result<T, TError> result in Guard.NotNull(results, nameof(results)))
            {
                if (result.IsSuccess)
                {
                    values.Add(result.Value);
                }
                else
                {
                    errors.Add(result.Error);
                }
            }

            return (values, errors);
        }
    }
}
