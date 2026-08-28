using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SpotifyPlaylistSorter.Pages.Auth;

[Authorize]
public sealed class LogoutModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Index");

    public IActionResult OnPost() => SignOut(
        new AuthenticationProperties { RedirectUri = Url.Page("/Index") },
        CookieAuthenticationDefaults.AuthenticationScheme);
}
