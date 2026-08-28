using SpotifyPlaylistSorter.Spotify;
using SpotifyPlaylistSorter.Spotify.Authentication;

namespace SpotifyPlaylistSorter.Tests.Spotify;

public sealed class SpotifyCurrentUserServiceTests
{
    [Fact]
    public async Task GetProfileAsync_UsesCurrentUsersAccessToken()
    {
        var apiClient = new FakeSpotifyApiClient();
        var tokenService = new FakeSpotifyTokenService();
        var service = new SpotifyCurrentUserService(apiClient, tokenService);

        var profile = await service.GetProfileAsync();

        Assert.Equal("account-id", profile.AccountId);
        Assert.Equal("current-access-token", apiClient.ReceivedAccessToken);
        Assert.Equal(1, tokenService.CallCount);
    }

    private sealed class FakeSpotifyTokenService : ISpotifyTokenService
    {
        public int CallCount { get; private set; }

        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult("current-access-token");
        }
    }

    private sealed class FakeSpotifyApiClient : ISpotifyApiClient
    {
        public string? ReceivedAccessToken { get; private set; }

        public Task<SpotifyUserProfile> GetCurrentUserProfileAsync(
            string accessToken,
            CancellationToken cancellationToken = default)
        {
            ReceivedAccessToken = accessToken;
            return Task.FromResult(new SpotifyUserProfile
            {
                AccountId = "account-id",
                Id = "legacy-id",
                Uri = "spotify:user:legacy-id"
            });
        }

        public Task<SpotifyPage<SpotifyPlaylistSummary>> GetCurrentUserPlaylistsPageAsync(
            string accessToken,
            int limit = 50,
            int offset = 0,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SpotifyPage<SpotifyPlaylistItem>> GetPlaylistItemsPageAsync(
            string accessToken,
            string playlistId,
            int limit = 50,
            int offset = 0,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
