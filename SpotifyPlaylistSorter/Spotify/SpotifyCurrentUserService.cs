using SpotifyPlaylistSorter.Spotify.Authentication;

namespace SpotifyPlaylistSorter.Spotify;

public sealed class SpotifyCurrentUserService(
    ISpotifyApiClient apiClient,
    ISpotifyTokenService tokenService) : ISpotifyCurrentUserService
{
    public async Task<SpotifyUserProfile> GetProfileAsync(
        CancellationToken cancellationToken = default)
    {
        var accessToken = await tokenService.GetAccessTokenAsync(cancellationToken);
        return await apiClient.GetCurrentUserProfileAsync(accessToken, cancellationToken);
    }
}
