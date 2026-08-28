namespace SpotifyPlaylistSorter.Spotify;

public interface ISpotifyPlaylistService
{
    Task<IReadOnlyList<SpotifyPlaylistSummary>> GetCurrentUserPlaylistsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SpotifyPlaylistItem>> GetPlaylistItemsAsync(
        string playlistId,
        CancellationToken cancellationToken = default);
}
