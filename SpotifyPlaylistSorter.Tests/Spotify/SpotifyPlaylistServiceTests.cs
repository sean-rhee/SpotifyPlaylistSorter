using SpotifyPlaylistSorter.Spotify;

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
        var service = new SpotifyPlaylistService(apiClient);

        var playlists = await service.GetCurrentUserPlaylistsAsync("token");

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
        var service = new SpotifyPlaylistService(apiClient);

        var items = await service.GetPlaylistItemsAsync("token", "playlist-id");

        Assert.Single(items);
        Assert.Equal([0, 1], apiClient.ItemOffsets);
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
    }
}
