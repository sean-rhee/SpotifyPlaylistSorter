namespace SpotifyPlaylistSorter.Spotify;

public interface ISpotifyApiClient
{
    Task<SpotifyUserProfile> GetCurrentUserProfileAsync(
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<SpotifyPage<SpotifyPlaylistSummary>> GetCurrentUserPlaylistsPageAsync(
        string accessToken,
        int limit = 50,
        int offset = 0,
        CancellationToken cancellationToken = default);

    Task<SpotifyPage<SpotifyPlaylistItem>> GetPlaylistItemsPageAsync(
        string accessToken,
        string playlistId,
        int limit = 50,
        int offset = 0,
        CancellationToken cancellationToken = default);
}
