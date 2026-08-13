namespace PayCalc24.Contracts.Identity;

/// <summary>Authenticated TS24 Core user; PayCalc24 does not own the user master.</summary>
public interface ICurrentUser
{
    UserId UserId { get; }
    bool HasPermission(string permissionCode);
}
