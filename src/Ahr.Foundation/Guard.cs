namespace Ahr.Foundation;

internal static class Guard
{
    internal static T NotNull<T>(T? value, string parameterName) => value is null ? throw new ArgumentNullException(parameterName) : value;

    internal static void ThrowIfUninitialized(ResultState state)
    {
        if (state == ResultState.Uninitialized)
        {
            throw new InvalidOperationException("The result is uninitialized. Create it with Success or Failure.");
        }
    }
}
