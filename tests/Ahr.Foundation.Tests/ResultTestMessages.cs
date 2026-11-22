namespace Ahr.Foundation.Tests;

internal static class ResultTestMessages
{
    internal const string Uninitialized = "The result is uninitialized. Create it with Success or Failure.";

    internal const string ErrorOfSuccess = "Cannot access the error of a successful result.";

    internal const string ValueOfFailure = "Cannot access the value of a failed result.";
}
