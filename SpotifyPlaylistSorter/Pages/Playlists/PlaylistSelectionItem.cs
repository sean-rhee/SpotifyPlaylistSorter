using SpotifyPlaylistSorter.Spotify;

namespace SpotifyPlaylistSorter.Pages.Playlists;

public sealed record PlaylistSelectionItem
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string SnapshotId { get; init; }

    public required string OwnerName { get; init; }

    public string? CoverImageUrl { get; init; }

    public string? SpotifyUrl { get; init; }

    public int? ItemCount { get; init; }

    public bool IsPublic { get; init; }

    public bool IsCollaborative { get; init; }

    public bool IsOwnedByCurrentUser { get; init; }

    public bool CanSort => IsOwnedByCurrentUser || IsCollaborative;

    public string AccessLabel => IsOwnedByCurrentUser
        ? "Owned by you"
        : IsCollaborative
            ? "Collaborative"
            : "View only";

    public static PlaylistSelectionItem Create(
        SpotifyPlaylistSummary playlist,
        SpotifyUserProfile currentUser)
    {
        ArgumentNullException.ThrowIfNull(playlist);
        ArgumentNullException.ThrowIfNull(currentUser);

        return new PlaylistSelectionItem
        {
            Id = playlist.Id,
            Name = playlist.Name,
            SnapshotId = playlist.SnapshotId,
            OwnerName = playlist.Owner?.DisplayName ?? "Unknown owner",
            CoverImageUrl = playlist.Images.FirstOrDefault()?.Url,
            SpotifyUrl = playlist.ExternalUrls.Spotify,
            ItemCount = playlist.Items?.Total,
            IsPublic = playlist.Public is true,
            IsCollaborative = playlist.Collaborative,
            IsOwnedByCurrentUser = IsSameUser(playlist.Owner, currentUser)
        };
    }

    private static bool IsSameUser(
        SpotifyPlaylistOwner? owner,
        SpotifyUserProfile currentUser)
    {
        if (owner is null)
        {
            return false;
        }

        return Matches(owner.AccountId, currentUser.AccountId) ||
               Matches(owner.Id, currentUser.Id) ||
               Matches(owner.AccountId, currentUser.Id) ||
               Matches(owner.Id, currentUser.AccountId);
    }

    private static bool Matches(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) &&
        !string.IsNullOrWhiteSpace(right) &&
        string.Equals(left, right, StringComparison.Ordinal);
}
