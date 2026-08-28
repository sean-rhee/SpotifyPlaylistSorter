namespace SpotifyPlaylistSorter.Spotify;

public sealed class SpotifyOptions
{
    public const string SectionName = "Spotify";

    public Uri ApiBaseAddress { get; init; } = new("https://api.spotify.com/v1/");

    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);
}
