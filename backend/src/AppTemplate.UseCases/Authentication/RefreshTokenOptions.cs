namespace AppTemplate.UseCases.Authentication;

/// <param name="SlidingLifetime">How long a refresh token stays valid without being used.</param>
/// <param name="AbsoluteLifetime">How long a sign-in session lasts at most, however often it refreshes.</param>
/// <param name="ReuseGracePeriod">
/// A token rotated this recently is answered with a conflict instead of being treated as reuse:
/// two tabs refreshing at the same moment both send it.
/// </param>
public sealed record RefreshTokenOptions(
    TimeSpan SlidingLifetime,
    TimeSpan AbsoluteLifetime,
    TimeSpan ReuseGracePeriod
);
