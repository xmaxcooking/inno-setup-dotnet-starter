using App.Domain;

namespace App.Infrastructure;

internal sealed class FakeDbContext : IAppDbContext
{
    public IQueryable<Customer> Customers { get; } = new List<Customer>
    {
        new(1, "Ava Nguyen", "ava.nguyen@example.com", "Switzerland", new DateOnly(2023, 3, 14)),
        new(2, "Liam Fischer", "liam.fischer@example.com", "Germany", new DateOnly(2023, 6, 2)),
        new(3, "Mia Rossi", "mia.rossi@example.com", "Italy", new DateOnly(2024, 1, 21)),
        new(4, "Noah Dubois", "noah.dubois@example.com", "France", new DateOnly(2024, 5, 9)),
        new(5, "Emma Keller", "emma.keller@example.com", "Austria", new DateOnly(2024, 11, 30)),
        new(6, "Lucas Silva", "lucas.silva@example.com", "Brazil", new DateOnly(2025, 2, 15)),
        new(7, "Sofia Jensen", "sofia.jensen@example.com", "Denmark", new DateOnly(2025, 9, 4))
    }.AsQueryable();
}