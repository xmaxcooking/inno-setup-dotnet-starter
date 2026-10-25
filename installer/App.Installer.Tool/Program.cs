using App.Infrastructure;
using System.CommandLine;

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
    var dbContext = new FakeDbContext();
    foreach (var country in dbContext.Customers.Select(c => c.Country).Distinct().OrderBy(c => c))
    {
        Console.WriteLine(country);
    }

    return 0;
});

listCustomersCommand.SetAction(parseResult =>
{
    var country = parseResult.GetValue(countryArgument);
    var dbContext = new FakeDbContext();
    foreach (var customer in dbContext.Customers.Where(c => c.Country == country).OrderBy(c => c.Name))
    {
        Console.WriteLine(customer.Name);
    }

    return 0;
});

return rootCommand.Parse(args).Invoke();