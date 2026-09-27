# Spotify Playlist Sorter

A small ASP.NET Core web app that reorders your Spotify playlists by artist and album, something the Spotify app doesn't do on its own.

Sign in with Spotify, pick a playlist, choose how you want it organized, preview the result side by side with the current order, then either save a sorted copy or update the original playlist in place.

## Features

- **Sort by artist.** Drag artists into the order you want (or use the ↑/↓ buttons, search, or "Sort artists A–Z"). Tracks are grouped by artist in that order and otherwise keep their relative positions.
- **Sort by album.** Uses your artist order, then sorts each artist's tracks by album release date (earliest first), album name, disc number and track number.
- **Preview before saving.** The current and proposed orders are shown side by side, with a count of how many tracks would move.
- **Two ways to save:**
  - **Create a sorted copy.** Makes a new private playlist (default name: `<Playlist> (Sorted)`) and leaves the original untouched.
  - **Update the original.** Reorders the existing playlist in Spotify after a confirmation prompt.
- **Safe writes.** Saving is rejected if the playlist changed in Spotify after the preview was built, so you never overwrite edits you haven't seen.

Only playlists you own or that are collaborative can be sorted. Others appear as view-only.

### Known limitations

- Spotify's API can't add local files to a playlist, so a **sorted copy leaves out local files**. Updating the original keeps them.
- A sorted copy can't be made if the playlist contains an unavailable (removed or region-locked) Spotify item. Updating the original still works.

## Tech stack

- .NET 10 / ASP.NET Core Razor Pages
- Spotify Web API with the OAuth 2.0 authorization code flow, cookie sessions and automatic token refresh
- Bootstrap and vanilla JavaScript on the front end
- xUnit for tests

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A Spotify account

### 1. Create a Spotify app

1. Go to the [Spotify Developer Dashboard](https://developer.spotify.com/dashboard) and create an app.
2. Under **Redirect URIs**, add:

   ```
   http://127.0.0.1:5003/signin-spotify
   ```

   Spotify requires a loopback IP (`127.0.0.1`) rather than `localhost` for local redirect URIs, which is why the app runs on `127.0.0.1:5003`.
3. Select **Web API** as the API you'll use.
4. Copy the app's **Client ID** and **Client Secret**.

A new Spotify app starts in development mode, where only accounts you add under **User Management** in the dashboard can sign in.

### 2. Store your credentials

The app reads credentials from configuration and refuses to start without them. For local development, use [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) so they never end up in the repo:

```bash
cd SpotifyPlaylistSorter
dotnet user-secrets set "Spotify:Authentication:ClientId" "<your-client-id>"
dotnet user-secrets set "Spotify:Authentication:ClientSecret" "<your-client-secret>"
```

### 3. Run it

```bash
dotnet run --project SpotifyPlaylistSorter --launch-profile http
```

Then open <http://127.0.0.1:5003> and sign in with Spotify.

You can also open `SpotifyPlaylistSorter.slnx` in Visual Studio and run the `http` profile.

## Configuration

Settings live under the `Spotify` section of `appsettings.json`. Any of them can be overridden with user secrets or environment variables (use `__` in place of `:`, e.g. `Spotify__Authentication__ClientId`).

| Key | Default | Description |
| --- | --- | --- |
| `Spotify:Authentication:ClientId` | *(required)* | Spotify app client ID |
| `Spotify:Authentication:ClientSecret` | *(required)* | Spotify app client secret |
| `Spotify:Authentication:CallbackPath` | `/signin-spotify` | OAuth callback path; must match your redirect URI |
| `Spotify:Authentication:RefreshBeforeExpiry` | `00:01:00` | How early to refresh the access token before it expires |
| `Spotify:ApiBaseAddress` | `https://api.spotify.com/v1/` | Spotify Web API base URL |
| `Spotify:RequestTimeout` | `00:00:30` | Timeout for Spotify API requests |

### Spotify permissions requested

| Scope | Why |
| --- | --- |
| `user-read-private` | Read your profile to show your name and tell which playlists you own |
| `playlist-read-private`, `playlist-read-collaborative` | List your playlists and read their tracks |
| `playlist-modify-public`, `playlist-modify-private` | Create sorted copies and reorder playlists |

## Running with Docker

A `Dockerfile` is included. Build from the repository root:

```bash
docker build -f SpotifyPlaylistSorter/Dockerfile -t spotify-playlist-sorter .
docker run -p 5003:8080 \
  -e Spotify__Authentication__ClientId=<your-client-id> \
  -e Spotify__Authentication__ClientSecret=<your-client-secret> \
  spotify-playlist-sorter
```

The redirect URI registered in your Spotify app must match the address you reach the container on (for the command above, `http://127.0.0.1:5003/signin-spotify`). When deploying somewhere real, serve it over HTTPS and pass the client secret through your host's secret store, never through `appsettings.json`.

## Running the tests

```bash
dotnet test
```

Tests cover the Spotify API client, token refresh, playlist service, and the playlist page models.

## Project structure

```
SpotifyPlaylistSorter/
├── Pages/
│   ├── Auth/            # Login / logout
│   ├── Playlists/       # Playlist picker and the sort/preview/save page
│   └── Shared/          # Layout
├── Spotify/             # Typed Spotify Web API client and playlist services
│   └── Authentication/  # OAuth handler, token storage and refresh
└── wwwroot/js/playlist-sorter.js   # Client-side sorting and preview
SpotifyPlaylistSorter.Tests/        # xUnit tests
```
