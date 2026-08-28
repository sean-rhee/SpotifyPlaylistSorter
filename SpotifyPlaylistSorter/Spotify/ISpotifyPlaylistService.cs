namespace SpotifyPlaylistSorter.Spotify;

public interface ISpotifyPlaylistService
{
    Task<IReadOnlyList<SpotifyPlaylistSummary>> GetCurrentUserPlaylistsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SpotifyPlaylistItem>> GetPlaylistItemsAsync(
        string playlistId,
        CancellationToken cancellationToken = default);

    Task<SpotifyPlaylistSummary> CreateSortedCopyAsync(
        string name,
        string description,
        IReadOnlyList<string> itemUris,
        CancellationToken cancellationToken = default);

    Task UpdatePlaylistOrderAsync(
        string playlistId,
        string snapshotId,
        IReadOnlyList<int> orderedOriginalPositions,
        CancellationToken cancellationToken = default);
}
