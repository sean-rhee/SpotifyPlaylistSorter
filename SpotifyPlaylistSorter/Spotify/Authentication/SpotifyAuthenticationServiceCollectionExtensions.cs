using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

namespace SpotifyPlaylistSorter.Spotify.Authentication;

public static class SpotifyAuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddSpotifyAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(SpotifyAuthenticationDefaults.OptionsSectionName);

        services
            .AddOptions<SpotifyAuthenticationOptions>()
            .Bind(section)
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ClientId),
                "Spotify:Authentication:ClientId is required. Store it with user-secrets.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ClientSecret),
                "Spotify:Authentication:ClientSecret is required. Store it with user-secrets.")
            .Validate(
                options => options.CallbackPath.StartsWith('/'),
                "Spotify:Authentication:CallbackPath must start with a slash.")
            .Validate(
                options => options.RefreshBeforeExpiry >= TimeSpan.Zero,
                "Spotify:Authentication:RefreshBeforeExpiry cannot be negative.")
            .ValidateOnStart();

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = SpotifyAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.Name = "SpotifyPlaylistSorter.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
            })
            .AddOAuth<OAuthOptions, SpotifyOAuthHandler>(
                SpotifyAuthenticationDefaults.AuthenticationScheme,
                SpotifyAuthenticationDefaults.DisplayName,
                options => ConfigureSpotifyOAuth(options, section));

        services.AddAuthorization();
        services.AddHttpContextAccessor();
        services.TryAddSingleton(TimeProvider.System);

        services.AddHttpClient(SpotifyAuthenticationDefaults.TokenHttpClientName, httpClient =>
        {
            httpClient.BaseAddress = new Uri("https://accounts.spotify.com/");
            httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        });
        services.AddScoped<ISpotifyTokenService, SpotifyTokenService>();

        return services;
    }

    private static void ConfigureSpotifyOAuth(OAuthOptions options, IConfigurationSection section)
    {
        options.ClientId = section[nameof(SpotifyAuthenticationOptions.ClientId)] ?? string.Empty;
        options.ClientSecret = section[nameof(SpotifyAuthenticationOptions.ClientSecret)] ?? string.Empty;
        options.CallbackPath = section[nameof(SpotifyAuthenticationOptions.CallbackPath)] ?? "/signin-spotify";
        options.AuthorizationEndpoint = SpotifyAuthenticationDefaults.AuthorizationEndpoint;
        options.TokenEndpoint = SpotifyAuthenticationDefaults.TokenEndpoint;
        options.UserInformationEndpoint = SpotifyAuthenticationDefaults.UserInformationEndpoint;
        options.SaveTokens = true;

        options.CorrelationCookie.HttpOnly = true;
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

        options.Scope.Clear();
        options.Scope.Add("user-read-private");
        options.Scope.Add("playlist-read-private");
        options.Scope.Add("playlist-read-collaborative");
        options.Scope.Add("playlist-modify-public");
        options.Scope.Add("playlist-modify-private");

        options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "account_id");
        options.ClaimActions.MapJsonKey(ClaimTypes.Name, "display_name");
        options.ClaimActions.MapJsonKey(SpotifyClaimTypes.LegacyUserId, "id");
        options.ClaimActions.MapJsonKey(SpotifyClaimTypes.ProfileUri, "uri");

        options.Events.OnCreatingTicket = async context =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);

            using var response = await context.Backchannel.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                context.HttpContext.RequestAborted);
            response.EnsureSuccessStatusCode();

            await using var responseStream = await response.Content.ReadAsStreamAsync(
                context.HttpContext.RequestAborted);
            using var user = await JsonDocument.ParseAsync(
                responseStream,
                cancellationToken: context.HttpContext.RequestAborted);

            context.RunClaimActions(user.RootElement);

            var identity = (ClaimsIdentity?)context.Principal?.Identity;
            if (identity is not null &&
                !identity.HasClaim(claim => claim.Type == ClaimTypes.NameIdentifier) &&
                user.RootElement.TryGetProperty("id", out var idProperty))
            {
                var legacyId = idProperty.GetString();
                if (!string.IsNullOrWhiteSpace(legacyId))
                {
                    identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, legacyId));
                }
            }
        };
    }
}
