using SpotifyPlaylistSorter.Spotify;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace SpotifyPlaylistSorter.Tests.Spotify;

public sealed class SpotifyApiClientTests
{
    [Fact]
    public async Task GetCurrentUserProfileAsync_SendsBearerTokenAndReadsSnakeCaseFields()
    {
        var handler = new StubHttpMessageHandler((_, _) => JsonResponse(
            """
            {
              "account_id": "stable-account-id",
              "display_name": "Test Listener",
              "id": "legacy-id",
              "uri": "spotify:user:legacy-id",
              "external_urls": { "spotify": "https://open.spotify.com/user/legacy-id" },
              "images": []
            }
            """));
        var client = CreateClient(handler);

        var profile = await client.GetCurrentUserProfileAsync("access-token");

        Assert.Equal("stable-account-id", profile.AccountId);
        Assert.Equal("Test Listener", profile.DisplayName);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://api.spotify.test/v1/me", request.Uri.AbsoluteUri);
        Assert.Equal("Bearer", request.AuthorizationScheme);
        Assert.Equal("access-token", request.AuthorizationParameter);
    }

    [Fact]
    public async Task GetPlaylistItemsPageAsync_UsesCurrentItemsEndpointAndReadsTrackMetadata()
    {
        var handler = new StubHttpMessageHandler((_, _) => JsonResponse(
            """
            {
              "href": "https://api.spotify.test/v1/playlists/playlist-id/items?offset=0&limit=50",
              "items": [
                {
                  "added_at": "2026-08-01T12:30:00Z",
                  "is_local": false,
                  "item": {
                    "album": {
                      "artists": [],
                      "external_urls": {},
                      "id": "album-id",
                      "images": [],
                      "name": "Album",
                      "release_date": "2026-01-02",
                      "release_date_precision": "day",
                      "uri": "spotify:album:album-id"
                    },
                    "artists": [
                      {
                        "external_urls": {},
                        "id": "artist-id",
                        "name": "Artist",
                        "uri": "spotify:artist:artist-id"
                      }
                    ],
                    "disc_number": 1,
                    "duration_ms": 123456,
                    "explicit": false,
                    "external_urls": {},
                    "id": "track-id",
                    "name": "Track",
                    "track_number": 4,
                    "type": "track",
                    "uri": "spotify:track:track-id"
                  }
                }
              ],
              "limit": 50,
              "next": null,
              "offset": 0,
              "previous": null,
              "total": 1
            }
            """));
        var client = CreateClient(handler);

        var page = await client.GetPlaylistItemsPageAsync("token", "playlist/id");

        var playlistItem = Assert.Single(page.Items);
        Assert.Equal("Track", playlistItem.Item?.Name);
        Assert.Equal("2026-01-02", playlistItem.Item?.Album?.ReleaseDate);
        Assert.Equal("Artist", Assert.Single(playlistItem.Item!.Artists).Name);
        Assert.Equal(
            "https://api.spotify.test/v1/playlists/playlist%2Fid/items?limit=50&offset=0",
            Assert.Single(handler.Requests).Uri.AbsoluteUri);
    }

    [Fact]
    public async Task GetCurrentUserPlaylistsPageAsync_RejectsInvalidPaginationBeforeSendingRequest()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new InvalidOperationException("No request should be sent."));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.GetCurrentUserPlaylistsPageAsync("token", limit: 51));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ApiError_ExposesSpotifyReasonAndRetryAfter()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            var response = JsonResponse(
                """
                {
                  "error": {
                    "status": 429,
                    "message": "Too many requests",
                    "reason": "QUOTA_EXCEEDED"
                  }
                }
                """,
                HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(7));
            return response;
        });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<SpotifyApiException>(() =>
            client.GetCurrentUserProfileAsync("token"));

        Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode);
        Assert.Equal("QUOTA_EXCEEDED", exception.Reason);
        Assert.Equal(TimeSpan.FromSeconds(7), exception.RetryAfter);
        Assert.Contains("Too many requests", exception.Message);
    }

    [Fact]
    public async Task Request_ObservesCancellation()
    {
        var handler = new StubHttpMessageHandler((_, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return JsonResponse("{}");
        });
        var client = CreateClient(handler);
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.GetCurrentUserProfileAsync("token", cancellationSource.Token));
    }

    private static SpotifyApiClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.spotify.test/v1/")
        });

    private static HttpResponseMessage JsonResponse(
        string json,
        HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpMessageHandler(
        Func<CapturedRequest, CancellationToken, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var capturedRequest = new CapturedRequest(
                request.Method,
                request.RequestUri ?? throw new InvalidOperationException("Request URI was missing."),
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter);
            Requests.Add(capturedRequest);

            return Task.FromResult(responseFactory(capturedRequest, cancellationToken));
        }
    }

    private sealed record CapturedRequest(
        HttpMethod Method,
        Uri Uri,
        string? AuthorizationScheme,
        string? AuthorizationParameter);
}
