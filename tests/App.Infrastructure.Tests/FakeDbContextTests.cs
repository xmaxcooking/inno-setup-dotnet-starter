namespace App.Infrastructure.Tests;

[TestClass]
public class FakeDbContextTests
{
    [TestMethod]
    public void Customers_ReturnsNonEmptyList()
    {
        var customers = new FakeDbContext().Customers.ToList();

        Assert.IsNotEmpty(customers);
    }

    [TestMethod]
    public void Customers_HasUniqueIds()
    {
        var customers = new FakeDbContext().Customers.ToList();

        Assert.HasCount(customers.Count, customers.Select(c => c.Id).Distinct());
    }
}