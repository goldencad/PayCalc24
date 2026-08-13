using PayCalc24.Contracts.Identity;

namespace PayCalc24.Contracts.Presentation;

/// <summary>Persistence port for user-owned presentation data only.</summary>
public interface IUserPresentationPreferenceStore
{
    ValueTask<UserPresentationPreferences?> GetAsync(UserId userId, CancellationToken cancellationToken = default);
    ValueTask SaveAsync(UserId userId, UserPresentationPreferences preferences, CancellationToken cancellationToken = default);
}
