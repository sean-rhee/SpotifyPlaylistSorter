namespace SpotifyPlaylistSorter.Spotify.Authentication;

public sealed class SpotifyAuthenticationOptions
{
    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    public string CallbackPath { get; init; } = "/signin-spotify";

    public TimeSpan RefreshBeforeExpiry { get; init; } = TimeSpan.FromMinutes(1);
}
