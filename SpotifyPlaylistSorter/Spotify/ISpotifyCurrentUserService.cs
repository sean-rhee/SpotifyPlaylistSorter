namespace SpotifyPlaylistSorter.Spotify;

public interface ISpotifyCurrentUserService
{
    Task<SpotifyUserProfile> GetProfileAsync(CancellationToken cancellationToken = default);
}
