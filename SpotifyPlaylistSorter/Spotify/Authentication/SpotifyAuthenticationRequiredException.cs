namespace SpotifyPlaylistSorter.Spotify.Authentication;

public class SpotifyAuthenticationRequiredException(string message) : InvalidOperationException(message);
