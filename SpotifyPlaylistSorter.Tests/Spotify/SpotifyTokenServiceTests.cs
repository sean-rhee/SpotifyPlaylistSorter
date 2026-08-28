using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SpotifyPlaylistSorter.Spotify.Authentication;
using System.Net;
using System.Security.Claims;
using System.Text;

namespace SpotifyPlaylistSorter.Tests.Spotify;

public sealed class SpotifyTokenServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetAccessTokenAsync_ReturnsCurrentTokenBeforeRefreshWindow()
    {
        var authentication = CreateAuthentication(
            accessToken: "current-access-token",
            refreshToken: "refresh-token",
            expiresAt: Now.AddMinutes(10));
        var authenticationService = new TestAuthenticationService(authentication);
        var handler = new StubHttpMessageHandler(() =>
            throw new InvalidOperationException("The token endpoint should not be called."));
        var service = CreateService(authenticationService, handler);

        var accessToken = await service.GetAccessTokenAsync();

        Assert.Equal("current-access-token", accessToken);
        Assert.Null(handler.Request);
        Assert.Null(authenticationService.SignedInProperties);
    }

    [Fact]
    public async Task GetAccessTokenAsync_RefreshesAndUpdatesAuthenticationTicket()
    {
        var authentication = CreateAuthentication(
            accessToken: "expired-access-token",
            refreshToken: "existing-refresh-token",
            expiresAt: Now.AddMinutes(-1));
        var authenticationService = new TestAuthenticationService(authentication);
        var handler = new StubHttpMessageHandler(() => JsonResponse(
            """
            {
              "access_token": "refreshed-access-token",
              "token_type": "Bearer",
              "expires_in": 3600,
              "scope": "playlist-read-private"
            }
            """));
        var service = CreateService(authenticationService, handler);

        var accessToken = await service.GetAccessTokenAsync();

        Assert.Equal("refreshed-access-token", accessToken);
        Assert.Equal(HttpMethod.Post, handler.Request?.Method);
        Assert.Equal("https://accounts.spotify.test/api/token", handler.Request?.Uri.AbsoluteUri);
        Assert.Equal("Basic", handler.Request?.AuthorizationScheme);
        Assert.Equal(
            Convert.ToBase64String(Encoding.UTF8.GetBytes("client-id:client-secret")),
            handler.Request?.AuthorizationParameter);
        Assert.Contains("grant_type=refresh_token", handler.Request?.Body);
        Assert.Contains("refresh_token=existing-refresh-token", handler.Request?.Body);

        var updatedProperties = Assert.IsType<AuthenticationProperties>(
            authenticationService.SignedInProperties);
        Assert.Equal("refreshed-access-token", updatedProperties.GetTokenValue("access_token"));
        Assert.Equal("existing-refresh-token", updatedProperties.GetTokenValue("refresh_token"));
        Assert.Equal("playlist-read-private", updatedProperties.GetTokenValue("scope"));
        Assert.Equal(Now.AddHours(1), DateTimeOffset.Parse(updatedProperties.GetTokenValue("expires_at")!));
    }

    [Fact]
    public async Task GetAccessTokenAsync_StoresRotatedRefreshToken()
    {
        var authentication = CreateAuthentication(
            accessToken: "expired-access-token",
            refreshToken: "old-refresh-token",
            expiresAt: Now.AddMinutes(-1));
        var authenticationService = new TestAuthenticationService(authentication);
        var handler = new StubHttpMessageHandler(() => JsonResponse(
            """
            {
              "access_token": "new-access-token",
              "token_type": "Bearer",
              "expires_in": 3600,
              "refresh_token": "new-refresh-token"
            }
            """));
        var service = CreateService(authenticationService, handler);

        await service.GetAccessTokenAsync();

        Assert.Equal(
            "new-refresh-token",
            authenticationService.SignedInProperties?.GetTokenValue("refresh_token"));
    }

    [Fact]
    public async Task GetAccessTokenAsync_InvalidGrantSignsOutAndRequiresReauthorization()
    {
        var authentication = CreateAuthentication(
            accessToken: "expired-access-token",
            refreshToken: "expired-refresh-token",
            expiresAt: Now.AddMinutes(-1));
        var authenticationService = new TestAuthenticationService(authentication);
        var handler = new StubHttpMessageHandler(() => JsonResponse(
            """
            {
              "error": "invalid_grant",
              "error_description": "Refresh token expired"
            }
            """,
            HttpStatusCode.BadRequest));
        var service = CreateService(authenticationService, handler);

        await Assert.ThrowsAsync<SpotifyReauthorizationRequiredException>(() =>
            service.GetAccessTokenAsync());

        Assert.Equal(1, authenticationService.SignOutCount);
        Assert.Null(authenticationService.SignedInProperties);
    }

    [Fact]
    public async Task GetAccessTokenAsync_UnauthenticatedUserRequiresLogin()
    {
        var authenticationService = new TestAuthenticationService(AuthenticateResult.NoResult());
        var handler = new StubHttpMessageHandler(() =>
            throw new InvalidOperationException("The token endpoint should not be called."));
        var service = CreateService(authenticationService, handler);

        await Assert.ThrowsAsync<SpotifyAuthenticationRequiredException>(() =>
            service.GetAccessTokenAsync());

        Assert.Null(handler.Request);
    }

    private static SpotifyTokenService CreateService(
        TestAuthenticationService authenticationService,
        HttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService>(authenticationService);
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };
        var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://accounts.spotify.test/")
        };

        return new SpotifyTokenService(
            httpContextAccessor,
            new TestHttpClientFactory(httpClient),
            Options.Create(new SpotifyAuthenticationOptions
            {
                ClientId = "client-id",
                ClientSecret = "client-secret",
                RefreshBeforeExpiry = TimeSpan.FromMinutes(1)
            }),
            new FixedTimeProvider(Now));
    }

    private static AuthenticateResult CreateAuthentication(
        string accessToken,
        string refreshToken,
        DateTimeOffset expiresAt)
    {
        var properties = new AuthenticationProperties();
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = "access_token", Value = accessToken },
            new AuthenticationToken { Name = "refresh_token", Value = refreshToken },
            new AuthenticationToken { Name = "token_type", Value = "Bearer" },
            new AuthenticationToken { Name = "expires_at", Value = expiresAt.ToString("O") }
        ]);
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "account-id")],
            CookieAuthenticationDefaults.AuthenticationScheme);
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(identity),
            properties,
            CookieAuthenticationDefaults.AuthenticationScheme);
        return AuthenticateResult.Success(ticket);
    }

    private static HttpResponseMessage JsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TestHttpClientFactory(HttpClient httpClient) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => httpClient;
    }

    private sealed class StubHttpMessageHandler(Func<HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        public CapturedRequest? Request { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = new CapturedRequest(
                request.Method,
                request.RequestUri ?? throw new InvalidOperationException("Request URI was missing."),
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken));
            return responseFactory();
        }
    }

    private sealed record CapturedRequest(
        HttpMethod Method,
        Uri Uri,
        string? AuthorizationScheme,
        string? AuthorizationParameter,
        string Body);

    private sealed class TestAuthenticationService(AuthenticateResult authenticateResult)
        : IAuthenticationService
    {
        public AuthenticationProperties? SignedInProperties { get; private set; }

        public int SignOutCount { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(authenticateResult);

        public Task ChallengeAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties) =>
            throw new NotSupportedException();

        public Task ForbidAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties) =>
            throw new NotSupportedException();

        public Task SignInAsync(
            HttpContext context,
            string? scheme,
            ClaimsPrincipal principal,
            AuthenticationProperties? properties)
        {
            SignedInProperties = properties;
            return Task.CompletedTask;
        }

        public Task SignOutAsync(
            HttpContext context,
            string? scheme,
            AuthenticationProperties? properties)
        {
            SignOutCount++;
            return Task.CompletedTask;
        }
    }
}
