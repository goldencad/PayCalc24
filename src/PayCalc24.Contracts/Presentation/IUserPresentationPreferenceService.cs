namespace PayCalc24.Contracts.Presentation;

public interface IUserPresentationPreferenceService
{
    ValueTask<UserPresentationPreferences> GetAsync(CancellationToken cancellationToken = default);
    ValueTask<UserPresentationPreferences> UpdateAsync(UserPresentationPreferences preferences, CancellationToken cancellationToken = default);
}
