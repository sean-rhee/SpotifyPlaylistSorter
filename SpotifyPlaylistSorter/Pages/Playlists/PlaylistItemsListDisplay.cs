namespace SpotifyPlaylistSorter.Pages.Playlists;

public sealed record PlaylistItemsListDisplay(
    IReadOnlyList<PlaylistItemDisplay> Items,
    string AriaLabel,
    bool ShowOriginalPosition = false,
    string? ElementId = null);
