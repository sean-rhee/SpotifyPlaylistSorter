using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SpotifyPlaylistSorter.Spotify;

public sealed class SpotifyApiClient(HttpClient httpClient) : ISpotifyApiClient
{
    private const int MaximumPageSize = 50;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public Task<SpotifyUserProfile> GetCurrentUserProfileAsync(
        string accessToken,
        CancellationToken cancellationToken = default) =>
        GetAsync<SpotifyUserProfile>("me", accessToken, cancellationToken);

    public Task<SpotifyPage<SpotifyPlaylistSummary>> GetCurrentUserPlaylistsPageAsync(
        string accessToken,
        int limit = MaximumPageSize,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        ValidatePagination(limit, offset);

        return GetAsync<SpotifyPage<SpotifyPlaylistSummary>>(
            FormattableString.Invariant($"me/playlists?limit={limit}&offset={offset}"),
            accessToken,
            cancellationToken);
    }

    public Task<SpotifyPage<SpotifyPlaylistItem>> GetPlaylistItemsPageAsync(
        string accessToken,
        string playlistId,
        int limit = MaximumPageSize,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playlistId);
        ValidatePagination(limit, offset);

        var escapedPlaylistId = Uri.EscapeDataString(playlistId);

        return GetAsync<SpotifyPage<SpotifyPlaylistItem>>(
            FormattableString.Invariant(
                $"playlists/{escapedPlaylistId}/items?limit={limit}&offset={offset}"),
            accessToken,
            cancellationToken);
    }

    public Task<SpotifyPlaylistSummary> CreatePlaylistAsync(
        string accessToken,
        string name,
        bool isPublic,
        string? description,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return SendJsonAsync<SpotifyPlaylistSummary>(
            HttpMethod.Post,
            "me/playlists",
            accessToken,
            new
            {
                name,
                @public = isPublic,
                collaborative = false,
                description
            },
            cancellationToken);
    }

    public Task<SpotifySnapshot> AddPlaylistItemsAsync(
        string accessToken,
        string playlistId,
        IReadOnlyList<string> itemUris,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playlistId);
        ArgumentNullException.ThrowIfNull(itemUris);
        if (itemUris.Count is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(itemUris),
                itemUris.Count,
                "Spotify accepts between 1 and 100 playlist items per request.");
        }

        return SendJsonAsync<SpotifySnapshot>(
            HttpMethod.Post,
            $"playlists/{Uri.EscapeDataString(playlistId)}/items",
            accessToken,
            new { uris = itemUris },
            cancellationToken);
    }

    public Task<SpotifySnapshot> ReorderPlaylistItemsAsync(
        string accessToken,
        string playlistId,
        int rangeStart,
        int insertBefore,
        int rangeLength,
        string snapshotId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playlistId);
        ArgumentOutOfRangeException.ThrowIfNegative(rangeStart);
        ArgumentOutOfRangeException.ThrowIfNegative(insertBefore);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rangeLength);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotId);

        return SendJsonAsync<SpotifySnapshot>(
            HttpMethod.Put,
            $"playlists/{Uri.EscapeDataString(playlistId)}/items",
            accessToken,
            new
            {
                rangeStart,
                insertBefore,
                rangeLength,
                snapshotId
            },
            cancellationToken);
    }

    private async Task<T> GetAsync<T>(
        string requestUri,
        string accessToken,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await CreateApiExceptionAsync(response, cancellationToken);
        }

        try
        {
            var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);

            return result ?? throw new JsonException("Spotify returned an empty JSON response.");
        }
        catch (JsonException exception)
        {
            throw new SpotifyApiException(
                response.StatusCode,
                "Spotify returned a response that the application could not understand.",
                reason: "INVALID_RESPONSE",
                retryAfter: null,
                responseBody: string.Empty,
                exception);
        }
    }

    private async Task<T> SendJsonAsync<T>(
        HttpMethod method,
        string requestUri,
        string accessToken,
        object body,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        using var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(body, options: JsonOptions);

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await CreateApiExceptionAsync(response, cancellationToken);
        }

        try
        {
            var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            return result ?? throw new JsonException("Spotify returned an empty JSON response.");
        }
        catch (JsonException exception)
        {
            throw new SpotifyApiException(
                response.StatusCode,
                "Spotify returned a response that the application could not understand.",
                reason: "INVALID_RESPONSE",
                retryAfter: null,
                responseBody: string.Empty,
                exception);
        }
    }

    private static async Task<SpotifyApiException> CreateApiExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        SpotifyErrorEnvelope? envelope = null;

        try
        {
            envelope = JsonSerializer.Deserialize<SpotifyErrorEnvelope>(responseBody, JsonOptions);
        }
        catch (JsonException)
        {
            // Preserve the raw response below when Spotify or an intermediary returns non-JSON.
        }

        var spotifyMessage = envelope?.Error?.Message;
        var reason = envelope?.Error?.Reason;
        var message = string.IsNullOrWhiteSpace(spotifyMessage)
            ? $"Spotify API request failed with status {(int)response.StatusCode} ({response.ReasonPhrase})."
            : $"Spotify API request failed with status {(int)response.StatusCode} ({response.ReasonPhrase}): {spotifyMessage}";

        return new SpotifyApiException(
            response.StatusCode,
            message,
            reason,
            GetRetryAfter(response),
            responseBody);
    }

    private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            return delta;
        }

        if (response.Headers.RetryAfter?.Date is not { } retryDate)
        {
            return null;
        }

        var delay = retryDate - DateTimeOffset.UtcNow;
        return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
    }

    private static void ValidatePagination(int limit, int offset)
    {
        if (limit is < 1 or > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                limit,
                $"Spotify page size must be between 1 and {MaximumPageSize}.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(offset);
    }

    private sealed record SpotifyErrorEnvelope
    {
        public SpotifyError? Error { get; init; }
    }

    private sealed record SpotifyError
    {
        public int Status { get; init; }

        public string? Message { get; init; }

        public string? Reason { get; init; }
    }
}
