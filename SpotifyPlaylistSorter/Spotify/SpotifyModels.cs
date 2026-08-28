namespace SpotifyPlaylistSorter.Spotify;

public sealed record SpotifyPage<T>
{
    public required string Href { get; init; }

    public required IReadOnlyList<T> Items { get; init; }

    public int Limit { get; init; }

    public string? Next { get; init; }

    public int Offset { get; init; }

    public string? Previous { get; init; }

    public int Total { get; init; }
}

public sealed record SpotifyUserProfile
{
    public string? AccountId { get; init; }

    public string? DisplayName { get; init; }

    public required string Id { get; init; }

    public required string Uri { get; init; }

    public SpotifyExternalUrls ExternalUrls { get; init; } = new();

    public IReadOnlyList<SpotifyImage> Images { get; init; } = [];
}

public sealed record SpotifyPlaylistSummary
{
    public bool Collaborative { get; init; }

    public string? Description { get; init; }

    public SpotifyExternalUrls ExternalUrls { get; init; } = new();

    public required string Id { get; init; }

    public IReadOnlyList<SpotifyImage> Images { get; init; } = [];

    public required string Name { get; init; }

    public SpotifyPlaylistOwner? Owner { get; init; }

    public bool? Public { get; init; }

    public required string SnapshotId { get; init; }

    public SpotifyCollectionSummary? Items { get; init; }

    public required string Uri { get; init; }
}

public sealed record SpotifyPlaylistOwner
{
    public string? AccountId { get; init; }

    public string? DisplayName { get; init; }

    public string? Id { get; init; }

    public string? Uri { get; init; }

    public SpotifyExternalUrls ExternalUrls { get; init; } = new();
}

public sealed record SpotifyCollectionSummary
{
    public string? Href { get; init; }

    public int Total { get; init; }
}

public sealed record SpotifyPlaylistItem
{
    public DateTimeOffset? AddedAt { get; init; }

    public SpotifyPlaylistOwner? AddedBy { get; init; }

    public bool IsLocal { get; init; }

    public SpotifyPlayableItem? Item { get; init; }
}

public sealed record SpotifyPlayableItem
{
    public SpotifyAlbum? Album { get; init; }

    public IReadOnlyList<SpotifyArtist> Artists { get; init; } = [];

    public int DiscNumber { get; init; }

    public int DurationMs { get; init; }

    public bool Explicit { get; init; }

    public SpotifyExternalUrls ExternalUrls { get; init; } = new();

    public string? Id { get; init; }

    public required string Name { get; init; }

    public int TrackNumber { get; init; }

    public required string Type { get; init; }

    public required string Uri { get; init; }
}

public sealed record SpotifyAlbum
{
    public IReadOnlyList<SpotifyArtist> Artists { get; init; } = [];

    public SpotifyExternalUrls ExternalUrls { get; init; } = new();

    public required string Id { get; init; }

    public IReadOnlyList<SpotifyImage> Images { get; init; } = [];

    public required string Name { get; init; }

    public string? ReleaseDate { get; init; }

    public string? ReleaseDatePrecision { get; init; }

    public required string Uri { get; init; }
}

public sealed record SpotifyArtist
{
    public SpotifyExternalUrls ExternalUrls { get; init; } = new();

    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Uri { get; init; }
}

public sealed record SpotifyExternalUrls
{
    public string? Spotify { get; init; }
}

public sealed record SpotifyImage
{
    public int? Height { get; init; }

    public required string Url { get; init; }

    public int? Width { get; init; }
}
