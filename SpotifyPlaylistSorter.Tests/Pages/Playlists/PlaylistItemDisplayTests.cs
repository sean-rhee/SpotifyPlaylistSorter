using SpotifyPlaylistSorter.Pages.Playlists;
using SpotifyPlaylistSorter.Spotify;

namespace SpotifyPlaylistSorter.Tests.Pages.Playlists;

public sealed class PlaylistItemDisplayTests
{
    [Fact]
    public void Create_MapsTrackMetadataAndPosition()
    {
        var playlistItem = new SpotifyPlaylistItem
        {
            AddedAt = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero),
            Item = new SpotifyPlayableItem
            {
                Album = new SpotifyAlbum
                {
                    Artists = [Artist("Album artist"), Artist("Guest album artist")],
                    Id = "album-id",
                    Images = [new SpotifyImage { Url = "https://image.test/album.jpg" }],
                    Name = "Album name",
                    ReleaseDate = "2004-03-23",
                    Uri = "spotify:album:album-id"
                },
                Artists =
                [
                    Artist("First artist"),
                    Artist("Second artist"),
                    Artist("First artist")
                ],
                DurationMs = 185_000,
                DiscNumber = 2,
                Explicit = true,
                ExternalUrls = new SpotifyExternalUrls
                {
                    Spotify = "https://open.spotify.test/track/track-id"
                },
                Id = "track-id",
                Name = "Track name",
                TrackNumber = 4,
                Type = "track",
                Uri = "spotify:track:track-id"
            }
        };

        var item = PlaylistItemDisplay.Create(playlistItem, position: 7);

        Assert.Equal(7, item.Position);
        Assert.Equal("Track name", item.Name);
        Assert.Equal("First artist, Second artist", item.CreatorName);
        Assert.Equal("Album name", item.AlbumName);
        Assert.Equal("Album artist", item.AlbumArtistName);
        Assert.Equal("2004-03-23", item.AlbumReleaseDate);
        Assert.Equal("https://image.test/album.jpg", item.AlbumImageUrl);
        Assert.Equal("3:05", item.Duration);
        Assert.Equal(2, item.DiscNumber);
        Assert.Equal(4, item.TrackNumber);
        Assert.Equal(7, item.OriginalPosition);
        Assert.True(item.IsExplicit);
        Assert.True(item.IsAvailable);
    }

    [Fact]
    public void Create_FormatsLongDurationWithHours()
    {
        var playlistItem = new SpotifyPlaylistItem
        {
            Item = PlayableItem(durationMs: 3_725_000)
        };

        var item = PlaylistItemDisplay.Create(playlistItem, position: 1);

        Assert.Equal("1:02:05", item.Duration);
    }

    [Fact]
    public void Create_UnavailableEntryPreservesItsPosition()
    {
        var playlistItem = new SpotifyPlaylistItem
        {
            AddedAt = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            Item = null
        };

        var item = PlaylistItemDisplay.Create(playlistItem, position: 12);

        Assert.Equal(12, item.Position);
        Assert.Equal("Unavailable item", item.Name);
        Assert.False(item.IsAvailable);
        Assert.Equal(playlistItem.AddedAt, item.AddedAt);
    }

    [Fact]
    public void Create_EpisodeWithoutArtistsUsesEpisodeLabel()
    {
        var playlistItem = new SpotifyPlaylistItem
        {
            Item = PlayableItem(type: "episode")
        };

        var item = PlaylistItemDisplay.Create(playlistItem, position: 1);

        Assert.Equal("Podcast episode", item.CreatorName);
        Assert.Equal("episode", item.ItemType);
    }

    [Fact]
    public void Create_LocalFileWithoutArtistsUsesLocalLabel()
    {
        var playlistItem = new SpotifyPlaylistItem
        {
            IsLocal = true,
            Item = PlayableItem()
        };

        var item = PlaylistItemDisplay.Create(playlistItem, position: 1);

        Assert.Equal("Local file", item.CreatorName);
        Assert.True(item.IsLocal);
    }

    private static SpotifyArtist Artist(string name) => new()
    {
        Id = name,
        Name = name,
        Uri = $"spotify:artist:{name}"
    };

    private static SpotifyPlayableItem PlayableItem(
        int durationMs = 180_000,
        string type = "track") => new()
        {
            DurationMs = durationMs,
            Id = "item-id",
            Name = "Item name",
            Type = type,
            Uri = $"spotify:{type}:item-id"
        };
}
