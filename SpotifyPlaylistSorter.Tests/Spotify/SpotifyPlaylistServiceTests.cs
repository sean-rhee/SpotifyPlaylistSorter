using SpotifyPlaylistSorter.Spotify;
using SpotifyPlaylistSorter.Spotify.Authentication;

namespace SpotifyPlaylistSorter.Tests.Spotify;

public sealed class SpotifyPlaylistServiceTests
{
    [Fact]
    public async Task GetCurrentUserPlaylistsAsync_CombinesAllPages()
    {
        var apiClient = new FakeSpotifyApiClient
        {
            PlaylistPages =
            [
                Page([Playlist("first"), Playlist("second")], offset: 0, next: "next-page"),
                Page([Playlist("third")], offset: 2, next: null)
            ]
        };
        var service = new SpotifyPlaylistService(apiClient, new FakeSpotifyTokenService());

        var playlists = await service.GetCurrentUserPlaylistsAsync();

        Assert.Equal(["first", "second", "third"], playlists.Select(playlist => playlist.Id));
        Assert.Equal([0, 2], apiClient.PlaylistOffsets);
    }

    [Fact]
    public async Task GetPlaylistItemsAsync_StopsOnAnEmptyPage()
    {
        var apiClient = new FakeSpotifyApiClient
        {
            ItemPages =
            [
                ItemPage([PlaylistItem("track-id")], offset: 0, next: "next-page"),
                ItemPage([], offset: 1, next: "unexpected-next-page")
            ]
        };
        var service = new SpotifyPlaylistService(apiClient, new FakeSpotifyTokenService());

        var items = await service.GetPlaylistItemsAsync("playlist-id");

        Assert.Single(items);
        Assert.Equal([0, 1], apiClient.ItemOffsets);
    }

    [Fact]
    public async Task CreateSortedCopyAsync_CreatesPrivatePlaylistAndAddsItemsInBatches()
    {
        var apiClient = new FakeSpotifyApiClient();
        var service = new SpotifyPlaylistService(apiClient, new FakeSpotifyTokenService());
        var itemUris = Enumerable.Range(1, 205)
            .Select(index => $"spotify:track:{index}")
            .ToArray();

        var playlist = await service.CreateSortedCopyAsync(
            "Sorted copy",
            "Created by the sorter",
            itemUris);

        Assert.Equal("created-playlist", playlist.Id);
        Assert.Equal("Sorted copy", apiClient.CreatedPlaylistName);
        Assert.False(apiClient.CreatedPlaylistIsPublic);
        Assert.Equal([100, 100, 5], apiClient.AddedItemBatches.Select(batch => batch.Count));
        Assert.Equal(itemUris, apiClient.AddedItemBatches.SelectMany(batch => batch));
    }

    [Fact]
    public async Task UpdatePlaylistOrderAsync_MovesMatchingContiguousRangesAndCarriesSnapshotForward()
    {
        var apiClient = new FakeSpotifyApiClient();
        var service = new SpotifyPlaylistService(apiClient, new FakeSpotifyTokenService());

        await service.UpdatePlaylistOrderAsync(
            "playlist-id",
            "snapshot-0",
            [2, 3, 1, 5, 4]);

        Assert.Equal(
            [
                new ReorderCall(1, 0, 2, "snapshot-0"),
                new ReorderCall(4, 3, 1, "snapshot-1")
            ],
            apiClient.ReorderCalls);
    }

    [Fact]
    public async Task UpdatePlaylistOrderAsync_RejectsIncompletePermutationBeforeCallingSpotify()
    {
        var apiClient = new FakeSpotifyApiClient();
        var service = new SpotifyPlaylistService(apiClient, new FakeSpotifyTokenService());

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdatePlaylistOrderAsync(
            "playlist-id",
            "snapshot-id",
            [2, 2]));

        Assert.Empty(apiClient.ReorderCalls);
    }

    private static SpotifyPlaylistSummary Playlist(string id) => new()
    {
        Id = id,
        Name = $"Playlist {id}",
        SnapshotId = $"snapshot-{id}",
        Uri = $"spotify:playlist:{id}"
    };

    private static SpotifyPlaylistItem PlaylistItem(string id) => new()
    {
        Item = new SpotifyPlayableItem
        {
            Id = id,
            Name = $"Track {id}",
            Type = "track",
            Uri = $"spotify:track:{id}"
        }
    };

    private static SpotifyPage<SpotifyPlaylistSummary> Page(
        IReadOnlyList<SpotifyPlaylistSummary> items,
        int offset,
        string? next) => new()
        {
            Href = "https://api.spotify.test/playlists",
            Items = items,
            Limit = 50,
            Next = next,
            Offset = offset,
            Total = 3
        };

    private static SpotifyPage<SpotifyPlaylistItem> ItemPage(
        IReadOnlyList<SpotifyPlaylistItem> items,
        int offset,
        string? next) => new()
        {
            Href = "https://api.spotify.test/items",
            Items = items,
            Limit = 50,
            Next = next,
            Offset = offset,
            Total = 1
        };

    private sealed class FakeSpotifyApiClient : ISpotifyApiClient
    {
        private int _playlistPageIndex;
        private int _itemPageIndex;

        public IReadOnlyList<SpotifyPage<SpotifyPlaylistSummary>> PlaylistPages { get; init; } = [];

        public IReadOnlyList<SpotifyPage<SpotifyPlaylistItem>> ItemPages { get; init; } = [];

        public List<int> PlaylistOffsets { get; } = [];

        public List<int> ItemOffsets { get; } = [];

        public string? CreatedPlaylistName { get; private set; }

        public bool CreatedPlaylistIsPublic { get; private set; }

        public List<IReadOnlyList<string>> AddedItemBatches { get; } = [];

        public List<ReorderCall> ReorderCalls { get; } = [];

        public Task<SpotifyUserProfile> GetCurrentUserProfileAsync(
            string accessToken,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SpotifyPage<SpotifyPlaylistSummary>> GetCurrentUserPlaylistsPageAsync(
            string accessToken,
            int limit = 50,
            int offset = 0,
            CancellationToken cancellationToken = default)
        {
            PlaylistOffsets.Add(offset);
            return Task.FromResult(PlaylistPages[_playlistPageIndex++]);
        }

        public Task<SpotifyPage<SpotifyPlaylistItem>> GetPlaylistItemsPageAsync(
            string accessToken,
            string playlistId,
            int limit = 50,
            int offset = 0,
            CancellationToken cancellationToken = default)
        {
            ItemOffsets.Add(offset);
            return Task.FromResult(ItemPages[_itemPageIndex++]);
        }

        public Task<SpotifyPlaylistSummary> CreatePlaylistAsync(
            string accessToken,
            string name,
            bool isPublic,
            string? description,
            CancellationToken cancellationToken = default)
        {
            CreatedPlaylistName = name;
            CreatedPlaylistIsPublic = isPublic;
            return Task.FromResult(Playlist("created-playlist") with
            {
                Name = name
            });
        }

        public Task<SpotifySnapshot> AddPlaylistItemsAsync(
            string accessToken,
            string playlistId,
            IReadOnlyList<string> itemUris,
            CancellationToken cancellationToken = default)
        {
            AddedItemBatches.Add(itemUris.ToArray());
            return Task.FromResult(new SpotifySnapshot
            {
                SnapshotId = $"add-snapshot-{AddedItemBatches.Count}"
            });
        }

        public Task<SpotifySnapshot> ReorderPlaylistItemsAsync(
            string accessToken,
            string playlistId,
            int rangeStart,
            int insertBefore,
            int rangeLength,
            string snapshotId,
            CancellationToken cancellationToken = default)
        {
            ReorderCalls.Add(new ReorderCall(rangeStart, insertBefore, rangeLength, snapshotId));
            return Task.FromResult(new SpotifySnapshot
            {
                SnapshotId = $"snapshot-{ReorderCalls.Count}"
            });
        }
    }

    private sealed record ReorderCall(
        int RangeStart,
        int InsertBefore,
        int RangeLength,
        string SnapshotId);

    private sealed class FakeSpotifyTokenService : ISpotifyTokenService
    {
        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult("token");
    }
}
