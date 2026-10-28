using App.Domain;
using App.Infrastructure;

namespace App.Core.Api;

internal sealed class CustomerDirectory : ICustomerDirectory
{
    private readonly IAppDbContext _dbContext;

    public CustomerDirectory(IAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IReadOnlyList<Customer> GetAll() => _dbContext.Customers.ToList();
}