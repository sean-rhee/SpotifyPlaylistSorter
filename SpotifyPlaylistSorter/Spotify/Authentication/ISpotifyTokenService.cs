namespace SpotifyPlaylistSorter.Spotify.Authentication;

public interface ISpotifyTokenService
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}
