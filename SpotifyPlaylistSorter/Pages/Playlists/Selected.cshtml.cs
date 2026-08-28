using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SpotifyPlaylistSorter.Spotify;
using SpotifyPlaylistSorter.Spotify.Authentication;
using System.Net;

namespace SpotifyPlaylistSorter.Pages.Playlists;

[Authorize]
public sealed class SelectedModel(
    ISpotifyCurrentUserService currentUserService,
    ISpotifyPlaylistService playlistService,
    ILogger<SelectedModel> logger) : PageModel
{
    public PlaylistSelectionItem? Playlist { get; private set; }

    public IReadOnlyList<PlaylistItemDisplay> Items { get; private set; } = [];

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        string playlistId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(playlistId))
        {
            return NotFound();
        }

        try
        {
            var profile = await currentUserService.GetProfileAsync(cancellationToken);
            var playlist = (await playlistService.GetCurrentUserPlaylistsAsync(cancellationToken))
                .FirstOrDefault(candidate => candidate.Id == playlistId);
            if (playlist is null)
            {
                return NotFound();
            }

            Playlist = PlaylistSelectionItem.Create(playlist, profile);
            if (!Playlist.CanSort)
            {
                return Forbid();
            }

            Items = (await playlistService.GetPlaylistItemsAsync(playlistId, cancellationToken))
                .Select((item, index) => PlaylistItemDisplay.Create(item, index + 1))
                .ToArray();
            return Page();
        }
        catch (SpotifyAuthenticationRequiredException exception)
        {
            logger.LogInformation(exception, "Spotify authorization must be renewed.");
            return RedirectToPage("/Auth/Login", new { returnUrl = $"/playlists/{Uri.EscapeDataString(playlistId)}" });
        }
        catch (SpotifyApiException exception) when (exception.StatusCode == HttpStatusCode.TooManyRequests)
        {
            logger.LogWarning(exception, "Spotify rate-limited playlist {PlaylistId}.", playlistId);
            ErrorMessage = exception.RetryAfter is { } retryAfter
                ? $"Spotify is receiving too many requests. Try again in about {Math.Ceiling(retryAfter.TotalSeconds)} seconds."
                : "Spotify is receiving too many requests. Please try again shortly.";
            return Playlist is null ? RedirectToPage("/Playlists/Index") : Page();
        }
        catch (SpotifyApiException exception)
        {
            logger.LogError(exception, "Spotify could not load playlist {PlaylistId}.", playlistId);
            ErrorMessage = "Spotify could not load this playlist's items. Please try again.";
            return Playlist is null ? RedirectToPage("/Playlists/Index") : Page();
        }
    }
}
