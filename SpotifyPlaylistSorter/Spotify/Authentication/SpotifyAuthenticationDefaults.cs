namespace SpotifyPlaylistSorter.Spotify.Authentication;

public static class SpotifyAuthenticationDefaults
{
    public const string AuthenticationScheme = "Spotify";
    public const string DisplayName = "Spotify";
    public const string OptionsSectionName = "Spotify:Authentication";
    public const string TokenHttpClientName = "SpotifyTokenEndpoint";

    public const string AuthorizationEndpoint = "https://accounts.spotify.com/authorize";
    public const string TokenEndpoint = "https://accounts.spotify.com/api/token";
    public const string UserInformationEndpoint = "https://api.spotify.com/v1/me";
}
