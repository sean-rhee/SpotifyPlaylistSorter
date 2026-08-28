using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SpotifyPlaylistSorter.Spotify;
using SpotifyPlaylistSorter.Spotify.Authentication;
using System.Net;

namespace SpotifyPlaylistSorter.Pages.Playlists;

[Authorize]
public sealed class IndexModel(
    ISpotifyCurrentUserService currentUserService,
    ISpotifyPlaylistService playlistService,
    ILogger<IndexModel> logger) : PageModel
{
    public string? DisplayName { get; private set; }

    public IReadOnlyList<PlaylistSelectionItem> Playlists { get; private set; } = [];

    public string? ErrorMessage { get; private set; }

    public int EditableCount => Playlists.Count(playlist => playlist.CanSort);

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            var profileTask = currentUserService.GetProfileAsync(cancellationToken);
            var playlistsTask = playlistService.GetCurrentUserPlaylistsAsync(cancellationToken);
            await Task.WhenAll(profileTask, playlistsTask);

            var profile = await profileTask;
            DisplayName = profile.DisplayName;
            Playlists = (await playlistsTask)
                .Select(playlist => PlaylistSelectionItem.Create(playlist, profile))
                .OrderByDescending(playlist => playlist.CanSort)
                .ThenBy(playlist => playlist.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            return Page();
        }
        catch (SpotifyAuthenticationRequiredException exception)
        {
            logger.LogInformation(exception, "Spotify authorization must be renewed.");
            return RedirectToPage("/Auth/Login", new { returnUrl = "/playlists" });
        }
        catch (SpotifyApiException exception) when (exception.StatusCode == HttpStatusCode.TooManyRequests)
        {
            logger.LogWarning(exception, "Spotify rate-limited the playlist request.");
            ErrorMessage = exception.RetryAfter is { } retryAfter
                ? $"Spotify is receiving too many requests. Try again in about {Math.Ceiling(retryAfter.TotalSeconds)} seconds."
                : "Spotify is receiving too many requests. Please try again shortly.";
            return Page();
        }
        catch (SpotifyApiException exception)
        {
            logger.LogError(exception, "Spotify could not load the current user's playlists.");
            ErrorMessage = "Spotify could not load your playlists. Please try again.";
            return Page();
        }
    }
}
