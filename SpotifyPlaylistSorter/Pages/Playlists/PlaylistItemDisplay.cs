using SpotifyPlaylistSorter.Spotify;

namespace SpotifyPlaylistSorter.Pages.Playlists;

public sealed record PlaylistItemDisplay
{
    public int Position { get; init; }

    public required string Name { get; init; }

    public required string CreatorName { get; init; }

    public string? AlbumName { get; init; }

    public string? AlbumImageUrl { get; init; }

    public string? SpotifyUrl { get; init; }

    public string? Duration { get; init; }

    public DateTimeOffset? AddedAt { get; init; }

    public bool IsExplicit { get; init; }

    public bool IsLocal { get; init; }

    public bool IsAvailable { get; init; }

    public string ItemType { get; init; } = "unknown";

    public static PlaylistItemDisplay Create(SpotifyPlaylistItem playlistItem, int position)
    {
        ArgumentNullException.ThrowIfNull(playlistItem);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(position);

        var item = playlistItem.Item;
        if (item is null)
        {
            return new PlaylistItemDisplay
            {
                Position = position,
                Name = "Unavailable item",
                CreatorName = "This item is no longer available from Spotify",
                AddedAt = playlistItem.AddedAt,
                IsLocal = playlistItem.IsLocal,
                IsAvailable = false
            };
        }

        var creatorName = string.Join(
            ", ",
            item.Artists
                .Select(artist => artist.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.CurrentCultureIgnoreCase));
        if (string.IsNullOrWhiteSpace(creatorName))
        {
            creatorName = item.Type switch
            {
                "episode" => "Podcast episode",
                _ when playlistItem.IsLocal => "Local file",
                _ => "Unknown artist"
            };
        }

        return new PlaylistItemDisplay
        {
            Position = position,
            Name = item.Name,
            CreatorName = creatorName,
            AlbumName = item.Album?.Name,
            AlbumImageUrl = item.Album?.Images.FirstOrDefault()?.Url,
            SpotifyUrl = item.ExternalUrls.Spotify,
            Duration = FormatDuration(item.DurationMs),
            AddedAt = playlistItem.AddedAt,
            IsExplicit = item.Explicit,
            IsLocal = playlistItem.IsLocal,
            IsAvailable = true,
            ItemType = item.Type
        };
    }

    private static string? FormatDuration(int durationMilliseconds)
    {
        if (durationMilliseconds <= 0)
        {
            return null;
        }

        var duration = TimeSpan.FromMilliseconds(durationMilliseconds);
        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{(int)duration.TotalMinutes}:{duration.Seconds:00}";
    }
}
