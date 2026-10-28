using App.Domain;

namespace App.Core.Api;

public interface ICustomerDirectory
{
    IReadOnlyList<Customer> GetAll();
}