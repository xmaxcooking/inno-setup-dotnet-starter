namespace App.Domain;

public sealed record Customer(int Id, string Name, string Email, string Country, DateOnly SignedUpOn);