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

    public IReadOnlyList<PlaylistItemDisplay> ProposedItems { get; private set; } = [];

    public IReadOnlyList<string> ArtistNames { get; private set; } = [];

    public int MovedItemCount => ProposedItems.Count(item => item.Position != item.OriginalPosition);

    public int LocalItemCount => Items.Count(item => item.IsLocal);

    public bool CanCreateCopy =>
        Items.Count > 0 &&
        Items.All(item => item.CanCopy || item.IsLocal);

    public string? ErrorMessage { get; private set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? SuccessSpotifyUrl { get; set; }

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
            return await LoadPageAsync(playlistId, cancellationToken) ?? Page();
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

    public async Task<IActionResult> OnPostCreateCopyAsync(
        string playlistId,
        string? orderedPositions,
        string? snapshotId,
        string? copyName,
        CancellationToken cancellationToken)
    {
        try
        {
            var loadResult = await LoadPageAsync(playlistId, cancellationToken);
            if (loadResult is not null)
            {
                return loadResult;
            }

            if (!TryValidateSaveRequest(orderedPositions, snapshotId, out var order))
            {
                return Page();
            }

            if (!CanCreateCopy)
            {
                ErrorMessage = "A sorted copy cannot be created because this playlist contains an unavailable Spotify item. You can still update the original playlist's order.";
                return Page();
            }

            var name = string.IsNullOrWhiteSpace(copyName)
                ? $"{Playlist!.Name} (Sorted)"
                : copyName.Trim();
            if (name.Length > 100)
            {
                ErrorMessage = "The copy name must be 100 characters or fewer.";
                return Page();
            }

            var itemUris = order
                .Select(position => Items[position - 1])
                .Where(item => item.CanCopy)
                .Select(item => item.SpotifyUri!)
                .ToArray();
            var createdPlaylist = await playlistService.CreateSortedCopyAsync(
                name,
                $"Sorted copy of {Playlist!.Name}, created by Spotify Playlist Sorter.",
                itemUris,
                cancellationToken);

            SuccessMessage = LocalItemCount == 0
                ? $"Created the private playlist “{createdPlaylist.Name}” in Spotify."
                : $"Created the private playlist “{createdPlaylist.Name}” without {FormatLocalFileCount(LocalItemCount)}. Add those local files again in Spotify if you want them in the copy.";
            SuccessSpotifyUrl = createdPlaylist.ExternalUrls.Spotify;
            return RedirectToPage(new { playlistId });
        }
        catch (SpotifyAuthenticationRequiredException exception)
        {
            logger.LogInformation(exception, "Spotify authorization must be renewed while creating a sorted playlist copy.");
            return RedirectToPage("/Auth/Login", new { returnUrl = $"/playlists/{Uri.EscapeDataString(playlistId)}" });
        }
        catch (SpotifyApiException exception)
        {
            logger.LogError(exception, "Spotify could not create a sorted copy of playlist {PlaylistId}.", playlistId);
            SetWriteError(exception, "Spotify could not finish the sorted copy. The original playlist was not changed, but an incomplete private copy may appear in Spotify.");
            return Playlist is null ? RedirectToPage("/Playlists/Index") : Page();
        }
    }

    public async Task<IActionResult> OnPostUpdateOriginalAsync(
        string playlistId,
        string? orderedPositions,
        string? snapshotId,
        CancellationToken cancellationToken)
    {
        try
        {
            var loadResult = await LoadPageAsync(playlistId, cancellationToken);
            if (loadResult is not null)
            {
                return loadResult;
            }

            if (!TryValidateSaveRequest(orderedPositions, snapshotId, out var order))
            {
                return Page();
            }

            await playlistService.UpdatePlaylistOrderAsync(
                playlistId,
                Playlist!.SnapshotId,
                order,
                cancellationToken);

            SuccessMessage = $"Updated “{Playlist.Name}” with the proposed order.";
            SuccessSpotifyUrl = Playlist.SpotifyUrl;
            return RedirectToPage(new { playlistId });
        }
        catch (SpotifyAuthenticationRequiredException exception)
        {
            logger.LogInformation(exception, "Spotify authorization must be renewed while updating playlist {PlaylistId}.", playlistId);
            return RedirectToPage("/Auth/Login", new { returnUrl = $"/playlists/{Uri.EscapeDataString(playlistId)}" });
        }
        catch (SpotifyApiException exception)
        {
            logger.LogError(exception, "Spotify could not update playlist {PlaylistId}.", playlistId);
            SetWriteError(exception, "Spotify could not finish updating this playlist. Reload it to see its current Spotify order before trying again.");
            return Playlist is null ? RedirectToPage("/Playlists/Index") : Page();
        }
    }

    private async Task<IActionResult?> LoadPageAsync(
        string playlistId,
        CancellationToken cancellationToken)
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
        ProposedItems = Items.Select(item => item with { }).ToArray();
        ArtistNames = Items
            .Where(item => item is { IsAvailable: true, ItemType: "track" })
            .Select(item => item.AlbumArtistName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        return null;
    }

    private bool TryValidateSaveRequest(
        string? orderedPositions,
        string? snapshotId,
        out int[] order)
    {
        order = [];
        if (!string.Equals(snapshotId, Playlist!.SnapshotId, StringComparison.Ordinal))
        {
            ErrorMessage = "This playlist changed in Spotify after the preview was built. Reload the page and review the new order before saving.";
            return false;
        }

        var parts = orderedPositions?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        if (parts.Length != Items.Count || parts.Any(part => !int.TryParse(part, out _)))
        {
            ErrorMessage = "The proposed order could not be verified. Reload the page and build the sort again.";
            return false;
        }

        order = parts.Select(int.Parse).ToArray();
        if (!order.Order().SequenceEqual(Enumerable.Range(1, Items.Count)))
        {
            ErrorMessage = "The proposed order is incomplete or contains duplicate items. Reload the page and build the sort again.";
            return false;
        }

        if (order.SequenceEqual(Enumerable.Range(1, Items.Count)))
        {
            ErrorMessage = "Choose a sort that changes the proposed order before saving it to Spotify.";
            return false;
        }

        return true;
    }

    private void SetWriteError(SpotifyApiException exception, string fallbackMessage)
    {
        ErrorMessage = exception.StatusCode == HttpStatusCode.TooManyRequests
            ? exception.RetryAfter is { } retryAfter
                ? $"Spotify is receiving too many requests. Try again in about {Math.Ceiling(retryAfter.TotalSeconds)} seconds."
                : "Spotify is receiving too many requests. Please try again shortly."
            : fallbackMessage;
    }

    private static string FormatLocalFileCount(int count) =>
        $"{count} local {(count == 1 ? "file" : "files")}";
}
