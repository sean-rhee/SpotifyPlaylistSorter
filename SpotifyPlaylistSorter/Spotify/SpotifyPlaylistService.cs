using SpotifyPlaylistSorter.Spotify.Authentication;

namespace SpotifyPlaylistSorter.Spotify;

public sealed class SpotifyPlaylistService(
    ISpotifyApiClient apiClient,
    ISpotifyTokenService tokenService) : ISpotifyPlaylistService
{
    private const int PageSize = 50;

    public async Task<IReadOnlyList<SpotifyPlaylistSummary>> GetCurrentUserPlaylistsAsync(
        CancellationToken cancellationToken = default)
    {
        var accessToken = await tokenService.GetAccessTokenAsync(cancellationToken);

        return await ReadAllPagesAsync(
            (offset, token) => apiClient.GetCurrentUserPlaylistsPageAsync(
                accessToken,
                PageSize,
                offset,
                token),
            cancellationToken);
    }

    public async Task<IReadOnlyList<SpotifyPlaylistItem>> GetPlaylistItemsAsync(
        string playlistId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playlistId);
        var accessToken = await tokenService.GetAccessTokenAsync(cancellationToken);

        return await ReadAllPagesAsync(
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
