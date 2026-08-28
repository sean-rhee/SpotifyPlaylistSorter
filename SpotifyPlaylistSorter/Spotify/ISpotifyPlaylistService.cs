namespace SpotifyPlaylistSorter.Spotify;

public interface ISpotifyPlaylistService
{
    Task<IReadOnlyList<SpotifyPlaylistSummary>> GetCurrentUserPlaylistsAsync(
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SpotifyPlaylistItem>> GetPlaylistItemsAsync(
        string accessToken,
        string playlistId,
        CancellationToken cancellationToken = default);
}
