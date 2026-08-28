namespace SpotifyPlaylistSorter.Spotify;

public sealed class SpotifyPlaylistService(ISpotifyApiClient apiClient) : ISpotifyPlaylistService
{
    private const int PageSize = 50;

    public Task<IReadOnlyList<SpotifyPlaylistSummary>> GetCurrentUserPlaylistsAsync(
        string accessToken,
        CancellationToken cancellationToken = default) =>
        ReadAllPagesAsync(
            (offset, token) => apiClient.GetCurrentUserPlaylistsPageAsync(
                accessToken,
                PageSize,
                offset,
                token),
            cancellationToken);

    public Task<IReadOnlyList<SpotifyPlaylistItem>> GetPlaylistItemsAsync(
        string accessToken,
        string playlistId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playlistId);

        return ReadAllPagesAsync(
            (offset, token) => apiClient.GetPlaylistItemsPageAsync(
                accessToken,
                playlistId,
                PageSize,
                offset,
                token),
            cancellationToken);
    }

    private static async Task<IReadOnlyList<T>> ReadAllPagesAsync<T>(
        Func<int, CancellationToken, Task<SpotifyPage<T>>> getPageAsync,
        CancellationToken cancellationToken)
    {
        List<T> results = [];
        var offset = 0;

        while (true)
        {
            var page = await getPageAsync(offset, cancellationToken);
            results.AddRange(page.Items);

            if (page.Next is null || page.Items.Count == 0)
            {
                return results;
            }

            offset = checked(offset + page.Items.Count);
        }
    }
}
