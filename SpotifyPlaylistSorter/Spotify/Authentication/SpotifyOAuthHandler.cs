using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace SpotifyPlaylistSorter.Spotify.Authentication;

public sealed class SpotifyOAuthHandler(
    IOptionsMonitor<OAuthOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : OAuthHandler<OAuthOptions>(options, logger, encoder)
{
    protected override async Task<OAuthTokenResponse> ExchangeCodeAsync(
        OAuthCodeExchangeContext context)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Options.TokenEndpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = CreateBasicAuthenticationHeader(
            Options.ClientId,
            Options.ClientSecret);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = context.Code,
            ["redirect_uri"] = context.RedirectUri
        });

        using var response = await Backchannel.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            Context.RequestAborted);
        var responseBody = await response.Content.ReadAsStringAsync(Context.RequestAborted);

        if (!response.IsSuccessStatusCode)
        {
            return OAuthTokenResponse.Failed(new HttpRequestException(
                $"Spotify authorization-code exchange failed with status {(int)response.StatusCode} ({response.ReasonPhrase})."));
        }

        try
        {
            return OAuthTokenResponse.Success(JsonDocument.Parse(responseBody));
        }
        catch (JsonException exception)
        {
            return OAuthTokenResponse.Failed(new InvalidOperationException(
                "Spotify returned an invalid token response.",
                exception));
        }
    }

    internal static AuthenticationHeaderValue CreateBasicAuthenticationHeader(
        string clientId,
        string clientSecret)
    {
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        return new AuthenticationHeaderValue("Basic", credentials);
    }
}
