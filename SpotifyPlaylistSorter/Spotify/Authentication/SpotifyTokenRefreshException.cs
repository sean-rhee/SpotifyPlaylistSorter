using System.Net;

namespace SpotifyPlaylistSorter.Spotify.Authentication;

public sealed class SpotifyTokenRefreshException : HttpRequestException
{
    public SpotifyTokenRefreshException(
        HttpStatusCode statusCode,
        string message,
        string? error,
        string responseBody,
        Exception? innerException = null)
        : base(message, innerException, statusCode)
    {
        Error = error;
        ResponseBody = responseBody;
    }

    public string? Error { get; }

    public string ResponseBody { get; }
}
