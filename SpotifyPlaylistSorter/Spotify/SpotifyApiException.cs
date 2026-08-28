using System.Net;

namespace SpotifyPlaylistSorter.Spotify;

public sealed class SpotifyApiException : HttpRequestException
{
    public SpotifyApiException(
        HttpStatusCode statusCode,
        string message,
        string? reason,
        TimeSpan? retryAfter,
        string responseBody,
        Exception? innerException = null)
        : base(message, innerException, statusCode)
    {
        Reason = reason;
        RetryAfter = retryAfter;
        ResponseBody = responseBody;
    }

    public string? Reason { get; }

    public TimeSpan? RetryAfter { get; }

    public string ResponseBody { get; }
}
