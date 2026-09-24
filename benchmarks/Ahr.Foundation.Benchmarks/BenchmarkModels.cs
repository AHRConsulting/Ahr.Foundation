namespace Ahr.Foundation.Benchmarks;

public sealed record Order(
    int Id,
    Customer Customer,
    IReadOnlyList<OrderLine> Lines);

public sealed record Customer(
    string Name,
    Address Address);

public sealed record Address(
    string City,
    string Country);

public sealed record OrderLine(
    string Sku,
    int Quantity);

public sealed record DomainError(
    string Code,
    ErrorContext Context);

public sealed record ErrorContext(
    string Operation,
    IReadOnlyList<string> Details);

internal static class BenchmarkModels
{
    internal static readonly Order _order =
        new(
            42,
            new Customer("Ada", new Address("London", "UK")),
            [new OrderLine("BOOK", 1), new OrderLine("PEN", 3)]);

    internal static readonly DomainError _error =
        new("gateway-timeout", new ErrorContext("submit-order", ["timeout"]));
}
