namespace SpotifyPlaylistSorter.Spotify.Authentication;

public sealed class SpotifyReauthorizationRequiredException(string message)
    : SpotifyAuthenticationRequiredException(message);
