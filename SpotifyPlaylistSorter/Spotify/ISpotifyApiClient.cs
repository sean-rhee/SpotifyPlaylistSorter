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

    Task<SpotifyPlaylistSummary> CreatePlaylistAsync(
        string accessToken,
        string name,
        bool isPublic,
        string? description,
        CancellationToken cancellationToken = default);

    Task<SpotifySnapshot> AddPlaylistItemsAsync(
        string accessToken,
        string playlistId,
        IReadOnlyList<string> itemUris,
        CancellationToken cancellationToken = default);

    Task<SpotifySnapshot> ReorderPlaylistItemsAsync(
        string accessToken,
        string playlistId,
        int rangeStart,
        int insertBefore,
        int rangeLength,
        string snapshotId,
        CancellationToken cancellationToken = default);
}
