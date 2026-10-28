using App.Core.Api;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;

var services = new ServiceCollection();
services.AddCoreApi();
using var provider = services.BuildServiceProvider();
var customerDirectory = provider.GetRequiredService<ICustomerDirectory>();

var countryArgument = new Argument<string>("country")
{
    Description = "The country to list sample customers for"
};

var listCountriesCommand = new Command("list-countries", "List the distinct countries in the sample data");

var listCustomersCommand = new Command("list-customers", "List sample customers for a country")
{
    Arguments = { countryArgument }
};

var rootCommand = new RootCommand("Install-time data provider for Setup.iss's custom wizard pages")
{
    Subcommands = { listCountriesCommand, listCustomersCommand }
};

listCountriesCommand.SetAction(_ =>
{
    foreach (var country in customerDirectory.GetAll().Select(c => c.Country).Distinct().OrderBy(c => c))
    {
        Console.WriteLine(country);
    }

    return 0;
});

listCustomersCommand.SetAction(parseResult =>
{
    var country = parseResult.GetValue(countryArgument);
    foreach (var customer in customerDirectory.GetAll().Where(c => c.Country == country).OrderBy(c => c.Name))
    {
        Console.WriteLine(customer.Name);
    }

    return 0;
});

return rootCommand.Parse(args).Invoke();