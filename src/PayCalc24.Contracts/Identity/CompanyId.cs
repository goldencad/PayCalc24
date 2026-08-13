namespace PayCalc24.Contracts.Identity;

public readonly record struct CompanyId(Guid Value)
{
    public static CompanyId From(Guid value) => value == Guid.Empty
        ? throw new ArgumentException("Company identifier cannot be empty.", nameof(value))
        : new CompanyId(value);
}
