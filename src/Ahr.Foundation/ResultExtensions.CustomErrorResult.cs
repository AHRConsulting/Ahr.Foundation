namespace Ahr.Foundation;

public static partial class ResultExtensions
{
    /// <summary>Creates a successful custom error result.</summary>
    public static Result<T, TError> ToSuccess<T, TError>(this T value) => Result<T, TError>.Success(value);
}
