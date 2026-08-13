namespace PayCalc24.Contracts.Identity;

public readonly record struct UserId(Guid Value)
{
    public static UserId From(Guid value) => value == Guid.Empty
        ? throw new ArgumentException("User identifier cannot be empty.", nameof(value))
        : new UserId(value);
}
