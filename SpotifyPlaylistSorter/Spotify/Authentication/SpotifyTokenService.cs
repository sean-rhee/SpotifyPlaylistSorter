using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace SpotifyPlaylistSorter.Spotify.Authentication;

public sealed class SpotifyTokenService(
    IHttpContextAccessor httpContextAccessor,
    IHttpClientFactory httpClientFactory,
    IOptions<SpotifyAuthenticationOptions> options,
    TimeProvider timeProvider) : ISpotifyTokenService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private string? _refreshedAccessToken;
    private DateTimeOffset _refreshedAccessTokenExpiresAt;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new SpotifyAuthenticationRequiredException(
                "A Spotify access token can only be retrieved during an authenticated HTTP request.");
        var authentication = await httpContext.AuthenticateAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        if (!authentication.Succeeded || authentication.Principal is null)
        {
            throw new SpotifyAuthenticationRequiredException(
                "The current user is not connected to Spotify.");
        }

        var accessToken = authentication.Properties?.GetTokenValue("access_token");
        var expiresAt = ParseExpiration(authentication.Properties?.GetTokenValue("expires_at"));
        if (IsUsable(accessToken, expiresAt))
        {
            return accessToken!;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (IsUsable(_refreshedAccessToken, _refreshedAccessTokenExpiresAt))
            {
                return _refreshedAccessToken!;
            }

            var refreshToken = authentication.Properties?.GetTokenValue("refresh_token");
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                throw new SpotifyReauthorizationRequiredException(
                    "The Spotify session cannot be refreshed. Connect the account again.");
            }

            return await RefreshAsync(
                httpContext,
                authentication,
                refreshToken,
                cancellationToken);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<string> RefreshAsync(
        HttpContext httpContext,
        AuthenticateResult authentication,
        string refreshToken,
        CancellationToken cancellationToken)
    {
        var authenticationOptions = options.Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/token");
        request.Headers.Authorization = SpotifyOAuthHandler.CreateBasicAuthenticationHeader(
            authenticationOptions.ClientId,
            authenticationOptions.ClientSecret);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        });

        var httpClient = httpClientFactory.CreateClient(
            SpotifyAuthenticationDefaults.TokenHttpClientName);
        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        SpotifyTokenResponse? tokenResponse;
        try
        {
            tokenResponse = JsonSerializer.Deserialize<SpotifyTokenResponse>(responseBody, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new SpotifyTokenRefreshException(
                response.StatusCode,
                "Spotify returned a token response that the application could not understand.",
                error: "invalid_response",
                responseBody,
                exception);
        }

        if (!response.IsSuccessStatusCode)
        {
            if (string.Equals(tokenResponse?.Error, "invalid_grant", StringComparison.Ordinal))
            {
                await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                throw new SpotifyReauthorizationRequiredException(
                    "Spotify authorization has expired or was revoked. Connect the account again.");
            }

            var description = tokenResponse?.ErrorDescription;
            var message = string.IsNullOrWhiteSpace(description)
                ? $"Spotify token refresh failed with status {(int)response.StatusCode} ({response.ReasonPhrase})."
                : $"Spotify token refresh failed: {description}";
            throw new SpotifyTokenRefreshException(
                response.StatusCode,
                message,
                tokenResponse?.Error,
                responseBody);
        }

        if (tokenResponse is null ||
            string.IsNullOrWhiteSpace(tokenResponse.AccessToken) ||
            tokenResponse.ExpiresIn <= 0)
        {
            throw new SpotifyTokenRefreshException(
                response.StatusCode,
                "Spotify returned an incomplete token response.",
                error: "invalid_response",
                responseBody);
        }

        var expiresAt = timeProvider.GetUtcNow().AddSeconds(tokenResponse.ExpiresIn);
        var properties = authentication.Properties ?? new AuthenticationProperties();
        var tokens = properties.GetTokens().ToList();

        SetToken(tokens, "access_token", tokenResponse.AccessToken);
        SetToken(tokens, "token_type", tokenResponse.TokenType ?? "Bearer");
        SetToken(tokens, "expires_at", expiresAt.ToString("O", CultureInfo.InvariantCulture));

        if (!string.IsNullOrWhiteSpace(tokenResponse.RefreshToken))
        {
            SetToken(tokens, "refresh_token", tokenResponse.RefreshToken);
        }

        if (!string.IsNullOrWhiteSpace(tokenResponse.Scope))
        {
            SetToken(tokens, "scope", tokenResponse.Scope);
        }

        properties.StoreTokens(tokens);
        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            authentication.Principal!,
            properties);

        _refreshedAccessToken = tokenResponse.AccessToken;
        _refreshedAccessTokenExpiresAt = expiresAt;
        return tokenResponse.AccessToken;
    }

    private bool IsUsable(string? accessToken, DateTimeOffset expiresAt) =>
        !string.IsNullOrWhiteSpace(accessToken) &&
        expiresAt > timeProvider.GetUtcNow().Add(options.Value.RefreshBeforeExpiry);

    private static DateTimeOffset ParseExpiration(string? value) =>
        DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var expiration)
                ? expiration
                : DateTimeOffset.MinValue;

    private static void SetToken(
        ICollection<AuthenticationToken> tokens,
        string name,
        string value)
    {
        var existingToken = tokens.FirstOrDefault(token => token.Name == name);
        if (existingToken is null)
        {
            tokens.Add(new AuthenticationToken { Name = name, Value = value });
        }
        else
        {
            existingToken.Value = value;
        }
    }

    private sealed record SpotifyTokenResponse
    {
        public string? AccessToken { get; init; }

        public string? TokenType { get; init; }

        public int ExpiresIn { get; init; }

        public string? RefreshToken { get; init; }

        public string? Scope { get; init; }

        public string? Error { get; init; }

        public string? ErrorDescription { get; init; }
    }
}
