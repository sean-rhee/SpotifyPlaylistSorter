using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace SpotifyPlaylistSorter.Spotify;

public static class SpotifyServiceCollectionExtensions
{
    public static IServiceCollection AddSpotifyApi(this IServiceCollection services)
    {
        services
            .AddOptions<SpotifyOptions>()
            .BindConfiguration(SpotifyOptions.SectionName)
            .Validate(
                options => options.ApiBaseAddress.IsAbsoluteUri,
                "Spotify:ApiBaseAddress must be an absolute URI.")
            .Validate(
                options => options.ApiBaseAddress.AbsoluteUri.EndsWith('/'),
                "Spotify:ApiBaseAddress must end with a slash.")
            .Validate(
                options => options.RequestTimeout > TimeSpan.Zero,
                "Spotify:RequestTimeout must be greater than zero.")
            .ValidateOnStart();

        services.AddHttpClient<ISpotifyApiClient, SpotifyApiClient>((serviceProvider, httpClient) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<SpotifyOptions>>().Value;

            httpClient.BaseAddress = options.ApiBaseAddress;
            httpClient.Timeout = options.RequestTimeout;
            httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        });

        services.AddScoped<ISpotifyPlaylistService, SpotifyPlaylistService>();
        services.AddScoped<ISpotifyCurrentUserService, SpotifyCurrentUserService>();

        return services;
    }
}
