namespace Ahr.Foundation.Samples;

public static class Program
{
    public static async Task<int> Main()
    {
        Console.WriteLine("=== Ahr.Foundation Railway-Oriented Programming Samples ===");
        Console.WriteLine();

        // 1. Synchronous Pipeline with Result<T>
        Console.WriteLine("--- Scenario 1: User Registration Pipeline ---");
        Result<User> validRegistration = RegisterUser("ada@example.com", 28);
        _ = validRegistration.Match(
            user => Console.WriteLine($"Success: Created user '{user.Email}' (Age: {user.Age})"),
            error => Console.WriteLine($"Failed: {error.Message}"));

        Result<User> invalidRegistration = RegisterUser("", -5);
        _ = invalidRegistration.Match(
            user => Console.WriteLine($"Success: Created user '{user.Email}'"),
            error => Console.WriteLine($"Expected Failure: {error.Message}"));
        Console.WriteLine();

        // 2. Asynchronous Pipeline with TaskCompositionExtensions
        Console.WriteLine("--- Scenario 2: Async Order Checkout Pipeline ---");
        Result<OrderConfirmation> checkoutResult = await ProcessOrderAsync("ORDER-1234", 150.00m);
        _ = checkoutResult.Match(
            confirmation => Console.WriteLine($"Order Confirmed: {confirmation.ConfirmationNumber} - Amount: ${confirmation.Amount:F2}"),
            error => Console.WriteLine($"Order Failed: {error.Message}"));
        Console.WriteLine();

        // 3. Option<T> for Safe Value Retrieval
        Console.WriteLine("--- Scenario 3: Option<T> Lookup ---");
        string[] tags = ["dotnet", "csharp", "functional"];
        Option<string> matchedTag = tags.FirstOrNone(t => t.StartsWith("c", StringComparison.OrdinalIgnoreCase));
        _ = matchedTag.Match(
            tag => Console.WriteLine($"Found tag: {tag}"),
            () => Console.WriteLine("Tag not found"));
        Console.WriteLine();

        // 4. LINQ Query Syntax over Result<T>
        Console.WriteLine("--- Scenario 4: LINQ Query Syntax ---");
        Result<User> queried = RegisterUserWithQuerySyntax("grace@example.com", 45);
        _ = queried.Match(
            user => Console.WriteLine($"Success: Created user '{user.Email}' (Age: {user.Age}) via query syntax"),
            error => Console.WriteLine($"Failed: {error.Message}"));

        Result<User> queriedFailure = RegisterUserWithQuerySyntax("alan@example.com", -1);
        _ = queriedFailure.Match(
            user => Console.WriteLine($"Success: Created user '{user.Email}'"),
            error => Console.WriteLine($"Expected Failure (short-circuited): {error.Message}"));
        Console.WriteLine();

        // 5. Result.TryAsync as a Boundary Exception Adapter
        Console.WriteLine("--- Scenario 5: Result.TryAsync Boundary Adapter ---");
        Result<Customer> foundCustomer = await Result.TryAsync(() => GetCustomerAsync("CUST-1"));
        _ = foundCustomer.Match(
            customer => Console.WriteLine($"Success: Loaded customer '{customer.Name}'"),
            error => Console.WriteLine($"Failed: {error.Message}"));

        Result<Customer> missingCustomer = await Result.TryAsync(() => GetCustomerAsync("MISSING"));
        _ = missingCustomer.Match(
            customer => Console.WriteLine($"Success: Loaded customer '{customer.Name}'"),
            error => Console.WriteLine($"Expected Failure: {error.Message}"));

        Result<Customer, CustomerLookupError> missingCustomerWithCustomError = await Result.TryAsync(
            () => GetCustomerAsync("MISSING"),
            ex => new CustomerLookupError(ex.Message));
        _ = missingCustomerWithCustomError.Match(
            customer => Console.WriteLine($"Success: Loaded customer '{customer.Name}'"),
            error => Console.WriteLine($"Expected Failure (custom error): {error.Reason}"));

        // GetValueOrDefault reads the success payload (or a fallback) without an explicit Match/IsSuccess check.
        Customer? recoveredCustomer = missingCustomer.GetValueOrDefault();
        Console.WriteLine($"GetValueOrDefault on a failure returns: {(recoveredCustomer is null ? "null" : recoveredCustomer.Name)}");
        Console.WriteLine();

        // 6. OrElse/OrElseAsync as a Fallback Combinator
        Console.WriteLine("--- Scenario 6: OrElse/OrElseAsync Fallback Lookup ---");
        // Task-composition OrElseAsync chains directly off the unawaited task, no intermediate await required.
        Result<Customer> customerOrCache = await Result.TryAsync(() => GetCustomerAsync("MISSING"))
            .OrElseAsync(() => Result.TryAsync(() => GetCustomerAsync("CUST-1")));
        _ = customerOrCache.Match(
            customer => Console.WriteLine($"OrElseAsync recovered customer from a secondary source: '{customer.Name}'"),
            error => Console.WriteLine($"OrElseAsync failure: {error.Message}"));
        Console.WriteLine();

        Console.WriteLine("All sample scenarios executed successfully.");
        return 0;
    }

    // Produces exactly the same pipeline as RegisterUser, expressed with query syntax.
    // Each clause can see the values bound before it, and a failure skips the remaining clauses.
    private static Result<User> RegisterUserWithQuerySyntax(string email, int age) => from validEmail in ValidateEmail(email)
                                                                                      from validAge in ValidateAge(age)
                                                                                      select new User(validEmail, validAge);

    private static Result<User> RegisterUser(string email, int age) => ValidateEmail(email)
            .Bind(validEmail => ValidateAge(age).Map(validAge => new User(validEmail, validAge)));

    private static Result<string> ValidateEmail(string email) => string.IsNullOrWhiteSpace(email) || !email.Contains('@') ? "Email is invalid.".ToFailure<string>() : email.ToSuccess();

    private static Result<int> ValidateAge(int age) => age < 18 ? "User must be at least 18 years old.".ToFailure<int>() : age.ToSuccess();

    private static async Task<Result<OrderConfirmation>> ProcessOrderAsync(string orderId, decimal amount) => await ValidateInventoryAsync(orderId)
            .BindAsync(_ => ChargePaymentAsync(orderId, amount))
            .BindAsync(SendConfirmationAsync);

    private static Task<Result<string>> ValidateInventoryAsync(string orderId) => Task.FromResult(orderId.ToSuccess());

    private static Task<Result<decimal>> ChargePaymentAsync(string orderId, decimal amount) => amount <= 0
            ? Task.FromResult($"Payment amount for order '{orderId}' must be positive.".ToFailure<decimal>())
            : Task.FromResult(amount.ToSuccess());

    private static Task<Result<OrderConfirmation>> SendConfirmationAsync(decimal amount)
    {
        var confirmation = new OrderConfirmation($"CONF-{Guid.NewGuid():N}"[..12].ToUpperInvariant(), amount);
        return Task.FromResult(confirmation.ToSuccess());
    }

    // Simulates a repository call that throws instead of returning a Result, the scenario
    // Result.TryAsync is designed to adapt at the boundary.
    private static Task<Customer> GetCustomerAsync(string customerId) => customerId == "CUST-1"
            ? Task.FromResult(new Customer(customerId, "Ada Lovelace"))
            : throw new InvalidOperationException($"Customer '{customerId}' was not found.");

    private sealed record User(string Email, int Age);
    private sealed record OrderConfirmation(string ConfirmationNumber, decimal Amount);
    private sealed record Customer(string Id, string Name);
    private sealed record CustomerLookupError(string Reason);
}
