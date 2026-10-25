using App.Domain;

namespace App.Infrastructure;

public interface IAppDbContext
{
    IQueryable<Customer> Customers { get; }
}