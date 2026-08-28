using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using SpotifyPlaylistSorter.Pages.Playlists;
using SpotifyPlaylistSorter.Spotify;

namespace SpotifyPlaylistSorter.Tests.Pages.Playlists;

public sealed class SelectedModelTests
{
    [Fact]
    public async Task OnGetAsync_LoadsItemsForSelectedEditablePlaylist()
    {
        var playlistService = new FakeSpotifyPlaylistService(
            Playlist(ownerAccountId: "current-account"),
            [PlaylistItem("first"), PlaylistItem("second")]);
        var page = new SelectedModel(
            new FakeCurrentUserService(),
            playlistService,
            NullLogger<SelectedModel>.Instance);

        var result = await page.OnGetAsync("playlist-id", CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("playlist-id", playlistService.RequestedPlaylistId);
        Assert.Equal([1, 2], page.Items.Select(item => item.Position));
        Assert.Equal(["first", "second"], page.Items.Select(item => item.Name));
    }

    [Fact]
    public async Task OnGetAsync_DoesNotLoadItemsForViewOnlyPlaylist()
    {
        var playlistService = new FakeSpotifyPlaylistService(
            Playlist(ownerAccountId: "another-account"),
            [PlaylistItem("first")]);
        var page = new SelectedModel(
            new FakeCurrentUserService(),
            playlistService,
            NullLogger<SelectedModel>.Instance);

        var result = await page.OnGetAsync("playlist-id", CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.Null(playlistService.RequestedPlaylistId);
        Assert.Empty(page.Items);
    }

    private static SpotifyPlaylistSummary Playlist(string ownerAccountId) => new()
    {
        Id = "playlist-id",
        Name = "Playlist",
        Owner = new SpotifyPlaylistOwner
        {
            AccountId = ownerAccountId,
            DisplayName = "Owner"
        },
        SnapshotId = "snapshot-id",
        Uri = "spotify:playlist:playlist-id"
    };

    private static SpotifyPlaylistItem PlaylistItem(string name) => new()
    {
        Item = new SpotifyPlayableItem
        {
            Id = name,
            Name = name,
            Type = "track",
            Uri = $"spotify:track:{name}"
        }
    };

    private sealed class FakeCurrentUserService : ISpotifyCurrentUserService
    {
        public Task<SpotifyUserProfile> GetProfileAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SpotifyUserProfile
            {
                AccountId = "current-account",
                Id = "current-id",
                Uri = "spotify:user:current-id"
            });
    }

    private sealed class FakeSpotifyPlaylistService(
        SpotifyPlaylistSummary playlist,
        IReadOnlyList<SpotifyPlaylistItem> items) : ISpotifyPlaylistService
    {
        public string? RequestedPlaylistId { get; private set; }

        public Task<IReadOnlyList<SpotifyPlaylistSummary>> GetCurrentUserPlaylistsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SpotifyPlaylistSummary>>([playlist]);

        public Task<IReadOnlyList<SpotifyPlaylistItem>> GetPlaylistItemsAsync(
            string playlistId,
            CancellationToken cancellationToken = default)
        {
            RequestedPlaylistId = playlistId;
            return Task.FromResult(items);
        }
    }
}
