using App.Domain;

namespace App.Infrastructure;

internal interface IAppDbContext
{
    IQueryable<Customer> Customers { get; }
}