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
        Assert.Equal(["first", "second"], page.ProposedItems.Select(item => item.Name));
        Assert.Equal([1, 2], page.ProposedItems.Select(item => item.Position));
        Assert.Equal(0, page.MovedItemCount);
    }

    [Fact]
    public async Task OnGetAsync_ListsDistinctAlbumArtistsInFirstAppearanceOrder()
    {
        var playlistService = new FakeSpotifyPlaylistService(
            Playlist(ownerAccountId: "current-account"),
            [
                PlaylistItem("first", "Beta"),
                PlaylistItem("second", "Alpha"),
                PlaylistItem("third", "beta")
            ]);
        var page = new SelectedModel(
            new FakeCurrentUserService(),
            playlistService,
            NullLogger<SelectedModel>.Instance);

        await page.OnGetAsync("playlist-id", CancellationToken.None);

        Assert.Equal(["Beta", "Alpha"], page.ArtistNames);
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

    [Fact]
    public async Task OnPostUpdateOriginalAsync_AppliesVerifiedProposedOrder()
    {
        var playlistService = new FakeSpotifyPlaylistService(
            Playlist(ownerAccountId: "current-account"),
            [PlaylistItem("first"), PlaylistItem("second")]);
        var page = new SelectedModel(
            new FakeCurrentUserService(),
            playlistService,
            NullLogger<SelectedModel>.Instance);

        var result = await page.OnPostUpdateOriginalAsync(
            "playlist-id",
            "2,1",
            "snapshot-id",
            CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("playlist-id", playlistService.UpdatedPlaylistId);
        Assert.Equal("snapshot-id", playlistService.UpdatedSnapshotId);
        Assert.Equal([2, 1], playlistService.UpdatedOrder);
    }

    [Fact]
    public async Task OnPostUpdateOriginalAsync_RejectsPreviewFromOlderSnapshot()
    {
        var playlistService = new FakeSpotifyPlaylistService(
            Playlist(ownerAccountId: "current-account"),
            [PlaylistItem("first"), PlaylistItem("second")]);
        var page = new SelectedModel(
            new FakeCurrentUserService(),
            playlistService,
            NullLogger<SelectedModel>.Instance);

        var result = await page.OnPostUpdateOriginalAsync(
            "playlist-id",
            "2,1",
            "older-snapshot",
            CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Null(playlistService.UpdatedPlaylistId);
        Assert.Contains("changed in Spotify", page.ErrorMessage);
    }

    [Fact]
    public async Task OnPostCreateCopyAsync_UsesProposedItemOrder()
    {
        var playlistService = new FakeSpotifyPlaylistService(
            Playlist(ownerAccountId: "current-account"),
            [PlaylistItem("first"), PlaylistItem("second")]);
        var page = new SelectedModel(
            new FakeCurrentUserService(),
            playlistService,
            NullLogger<SelectedModel>.Instance);

        var result = await page.OnPostCreateCopyAsync(
            "playlist-id",
            "2,1",
            "snapshot-id",
            "My sorted copy",
            CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("My sorted copy", playlistService.CreatedCopyName);
        Assert.Equal(
            ["spotify:track:second", "spotify:track:first"],
            playlistService.CreatedCopyItemUris);
    }

    [Fact]
    public async Task OnPostCreateCopyAsync_OmitsLocalFilesAndReportsThem()
    {
        var playlistService = new FakeSpotifyPlaylistService(
            Playlist(ownerAccountId: "current-account"),
            [PlaylistItem("local", isLocal: true), PlaylistItem("spotify")]);
        var page = new SelectedModel(
            new FakeCurrentUserService(),
            playlistService,
            NullLogger<SelectedModel>.Instance);

        var result = await page.OnPostCreateCopyAsync(
            "playlist-id",
            "2,1",
            "snapshot-id",
            "Copy with local file",
            CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.True(page.CanCreateCopy);
        Assert.Equal(1, page.LocalItemCount);
        Assert.Equal(["spotify:track:spotify"], playlistService.CreatedCopyItemUris);
        Assert.Contains("without 1 local file", page.SuccessMessage);
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

    private static SpotifyPlaylistItem PlaylistItem(
        string name,
        string? albumArtist = null,
        bool isLocal = false) => new()
    {
        IsLocal = isLocal,
        Item = new SpotifyPlayableItem
        {
            Album = albumArtist is null
                ? null
                : new SpotifyAlbum
                {
                    Artists =
                    [
                        new SpotifyArtist
                        {
                            Id = albumArtist,
                            Name = albumArtist,
                            Uri = $"spotify:artist:{albumArtist}"
                        }
                    ],
                    Id = $"album-{name}",
                    Name = $"Album {name}",
                    Uri = $"spotify:album:{name}"
                },
            Id = name,
            Name = name,
            Type = "track",
            Uri = isLocal ? $"spotify:local:{name}" : $"spotify:track:{name}"
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

        public string? UpdatedPlaylistId { get; private set; }

        public string? UpdatedSnapshotId { get; private set; }

        public IReadOnlyList<int>? UpdatedOrder { get; private set; }

        public string? CreatedCopyName { get; private set; }

        public IReadOnlyList<string>? CreatedCopyItemUris { get; private set; }

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

        public Task<SpotifyPlaylistSummary> CreateSortedCopyAsync(
            string name,
            string description,
            IReadOnlyList<string> itemUris,
            CancellationToken cancellationToken = default)
        {
            CreatedCopyName = name;
            CreatedCopyItemUris = itemUris.ToArray();
            return Task.FromResult(Playlist("current-account") with
            {
                Id = "created-playlist-id",
                Name = name,
                ExternalUrls = new SpotifyExternalUrls
                {
                    Spotify = "https://open.spotify.com/playlist/created-playlist-id"
                }
            });
        }

        public Task UpdatePlaylistOrderAsync(
            string playlistId,
            string snapshotId,
            IReadOnlyList<int> orderedOriginalPositions,
            CancellationToken cancellationToken = default)
        {
            UpdatedPlaylistId = playlistId;
            UpdatedSnapshotId = snapshotId;
            UpdatedOrder = orderedOriginalPositions.ToArray();
            return Task.CompletedTask;
        }
    }
}
