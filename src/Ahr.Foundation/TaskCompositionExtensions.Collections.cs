namespace Ahr.Foundation;

public static partial class TaskCompositionExtensions
{
    // Ordinary extension methods: a second non-generic extension block in this class trips CA1708 on the compiler-generated names.
    /// <summary>Awaits each task in order and returns success, or the first failure; tasks after it are not awaited.</summary>
    public static async Task<Result> SequenceAsync(this IEnumerable<Task<Result>> tasks)
    {
        foreach (Task<Result> task in Guard.NotNull(tasks, nameof(tasks)))
        {
            Result result = await Guard.NotNull(task, nameof(tasks)).ConfigureAwait(false);
            if (result.IsFailure)
            {
                return result;
            }
        }

        return Result.Success();
    }

    /// <summary>Awaits every task in order and returns every error; an empty list means every result succeeded.</summary>
    public static async Task<IReadOnlyList<Error>> PartitionAsync(this IEnumerable<Task<Result>> tasks)
    {
        List<Error> errors = [];
        foreach (Task<Result> task in Guard.NotNull(tasks, nameof(tasks)))
        {
            Result result = await Guard.NotNull(task, nameof(tasks)).ConfigureAwait(false);
            if (result.IsFailure)
            {
                errors.Add(result.Error);
            }
        }

        return errors;
    }

    extension<T>(IEnumerable<Task<Result<T>>> tasks)
    {
        /// <summary>Awaits each task in order and returns all the values, or the first failure; tasks after it are not awaited.</summary>
        public async Task<Result<IReadOnlyList<T>>> SequenceAsync()
        {
            List<T> values = [];
            foreach (Task<Result<T>> task in Guard.NotNull(tasks, nameof(tasks)))
            {
                Result<T> result = await Guard.NotNull(task, nameof(tasks)).ConfigureAwait(false);
                if (result.IsFailure)
                {
                    return Result<IReadOnlyList<T>>.Failure(result.Error);
                }

                values.Add(result.Value);
            }

            return Result<IReadOnlyList<T>>.Success(values);
        }

        /// <summary>Awaits every task in order and returns the values and the errors separately.</summary>
        public async Task<(IReadOnlyList<T> Values, IReadOnlyList<Error> Errors)> PartitionAsync()
        {
            List<T> values = [];
            List<Error> errors = [];
            foreach (Task<Result<T>> task in Guard.NotNull(tasks, nameof(tasks)))
            {
                Result<T> result = await Guard.NotNull(task, nameof(tasks)).ConfigureAwait(false);
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

    extension<T, TError>(IEnumerable<Task<Result<T, TError>>> tasks)
    {
        /// <summary>Awaits each task in order and returns all the values, or the first failure; tasks after it are not awaited.</summary>
        public async Task<Result<IReadOnlyList<T>, TError>> SequenceAsync()
        {
            List<T> values = [];
            foreach (Task<Result<T, TError>> task in Guard.NotNull(tasks, nameof(tasks)))
            {
                Result<T, TError> result = await Guard.NotNull(task, nameof(tasks)).ConfigureAwait(false);
                if (result.IsFailure)
                {
                    return Result<IReadOnlyList<T>, TError>.Failure(result.Error);
                }

                values.Add(result.Value);
            }

            return Result<IReadOnlyList<T>, TError>.Success(values);
        }

        /// <summary>Awaits every task in order and returns the values and the errors separately.</summary>
        public async Task<(IReadOnlyList<T> Values, IReadOnlyList<TError> Errors)> PartitionAsync()
        {
            List<T> values = [];
            List<TError> errors = [];
            foreach (Task<Result<T, TError>> task in Guard.NotNull(tasks, nameof(tasks)))
            {
                Result<T, TError> result = await Guard.NotNull(task, nameof(tasks)).ConfigureAwait(false);
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
