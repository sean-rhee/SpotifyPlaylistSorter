using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SpotifyPlaylistSorter.Spotify.Authentication;

namespace SpotifyPlaylistSorter.Pages.Auth;

[AllowAnonymous]
public sealed class LoginModel : PageModel
{
    public IActionResult OnGet(string? returnUrl = null)
    {
        var destination = Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
        var properties = new AuthenticationProperties { RedirectUri = destination };

        return Challenge(properties, SpotifyAuthenticationDefaults.AuthenticationScheme);
    }
}
