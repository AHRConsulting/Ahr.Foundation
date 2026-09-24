namespace Ahr.Foundation;

internal enum ResultState : byte
{
    Uninitialized,
    Success,
    Failure,
}

internal static class ResultStateExtensions
{
    extension(ResultState state)
    {
        internal bool IsSuccess => state == ResultState.Success;
        internal bool IsFailure => state == ResultState.Failure;
    }
}
