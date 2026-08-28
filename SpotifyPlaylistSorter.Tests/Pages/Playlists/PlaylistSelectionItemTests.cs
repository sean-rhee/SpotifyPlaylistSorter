using SpotifyPlaylistSorter.Pages.Playlists;
using SpotifyPlaylistSorter.Spotify;

namespace SpotifyPlaylistSorter.Tests.Pages.Playlists;

public sealed class PlaylistSelectionItemTests
{
    [Fact]
    public void Create_OwnedPlaylistCanBeSorted()
    {
        var playlist = Playlist(ownerAccountId: "current-account");

        var item = PlaylistSelectionItem.Create(playlist, CurrentUser());

        Assert.True(item.IsOwnedByCurrentUser);
        Assert.True(item.CanSort);
        Assert.Equal("Owned by you", item.AccessLabel);
    }

    [Fact]
    public void Create_CollaborativePlaylistCanBeSorted()
    {
        var playlist = Playlist(ownerAccountId: "another-account") with
        {
            Collaborative = true
        };

        var item = PlaylistSelectionItem.Create(playlist, CurrentUser());

        Assert.False(item.IsOwnedByCurrentUser);
        Assert.True(item.CanSort);
        Assert.Equal("Collaborative", item.AccessLabel);
    }

    [Fact]
    public void Create_FollowedPlaylistIsViewOnly()
    {
        var playlist = Playlist(ownerAccountId: "another-account");

        var item = PlaylistSelectionItem.Create(playlist, CurrentUser());

        Assert.False(item.CanSort);
        Assert.Equal("View only", item.AccessLabel);
    }

    [Fact]
    public void Create_MapsDisplayMetadata()
    {
        var playlist = Playlist(ownerAccountId: "current-account") with
        {
            Images = [new SpotifyImage { Url = "https://image.test/cover.jpg" }],
            Items = new SpotifyCollectionSummary { Total = 42 },
            Public = true,
            ExternalUrls = new SpotifyExternalUrls
            {
                Spotify = "https://open.spotify.test/playlist/playlist-id"
            }
        };

        var item = PlaylistSelectionItem.Create(playlist, CurrentUser());

        Assert.Equal("https://image.test/cover.jpg", item.CoverImageUrl);
        Assert.Equal(42, item.ItemCount);
        Assert.True(item.IsPublic);
        Assert.Equal("Playlist owner", item.OwnerName);
    }

    private static SpotifyUserProfile CurrentUser() => new()
    {
        AccountId = "current-account",
        Id = "current-legacy-id",
        Uri = "spotify:user:current-legacy-id"
    };

    private static SpotifyPlaylistSummary Playlist(string ownerAccountId) => new()
    {
        Id = "playlist-id",
        Name = "Playlist name",
        Owner = new SpotifyPlaylistOwner
        {
            AccountId = ownerAccountId,
            DisplayName = "Playlist owner"
        },
        SnapshotId = "snapshot-id",
        Uri = "spotify:playlist:playlist-id"
    };
}
