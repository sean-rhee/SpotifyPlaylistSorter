using SpotifyPlaylistSorter.Spotify.Authentication;

namespace SpotifyPlaylistSorter.Spotify;

public sealed class SpotifyPlaylistService(
    ISpotifyApiClient apiClient,
    ISpotifyTokenService tokenService) : ISpotifyPlaylistService
{
    private const int PageSize = 50;
    private const int WriteBatchSize = 100;

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

    public async Task<SpotifyPlaylistSummary> CreateSortedCopyAsync(
        string name,
        string description,
        IReadOnlyList<string> itemUris,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(itemUris);

        var accessToken = await tokenService.GetAccessTokenAsync(cancellationToken);
        var playlist = await apiClient.CreatePlaylistAsync(
            accessToken,
            name,
            isPublic: false,
            description,
            cancellationToken);

        foreach (var batch in itemUris.Chunk(WriteBatchSize))
        {
            await apiClient.AddPlaylistItemsAsync(
                accessToken,
                playlist.Id,
                batch,
                cancellationToken);
        }

        return playlist;
    }

    public async Task UpdatePlaylistOrderAsync(
        string playlistId,
        string snapshotId,
        IReadOnlyList<int> orderedOriginalPositions,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playlistId);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotId);
        ArgumentNullException.ThrowIfNull(orderedOriginalPositions);
        if (!orderedOriginalPositions.Order().SequenceEqual(
                Enumerable.Range(1, orderedOriginalPositions.Count)))
        {
            throw new ArgumentException(
                "The proposed order must contain every original position exactly once.",
                nameof(orderedOriginalPositions));
        }

        var accessToken = await tokenService.GetAccessTokenAsync(cancellationToken);
        var currentOrder = Enumerable.Range(1, orderedOriginalPositions.Count).ToList();
        var currentSnapshotId = snapshotId;

        for (var targetIndex = 0; targetIndex < orderedOriginalPositions.Count; targetIndex++)
        {
            var currentIndex = currentOrder.IndexOf(orderedOriginalPositions[targetIndex]);
            if (currentIndex == targetIndex)
            {
                continue;
            }

            var rangeLength = 1;
            while (currentIndex + rangeLength < currentOrder.Count &&
                   targetIndex + rangeLength < orderedOriginalPositions.Count &&
                   currentOrder[currentIndex + rangeLength] == orderedOriginalPositions[targetIndex + rangeLength])
            {
                rangeLength++;
            }

            var snapshot = await apiClient.ReorderPlaylistItemsAsync(
                accessToken,
                playlistId,
                currentIndex,
                targetIndex,
                rangeLength,
                currentSnapshotId,
                cancellationToken);
            currentSnapshotId = snapshot.SnapshotId;

            var movedItems = currentOrder.GetRange(currentIndex, rangeLength);
            currentOrder.RemoveRange(currentIndex, rangeLength);
            currentOrder.InsertRange(targetIndex, movedItems);
        }
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
