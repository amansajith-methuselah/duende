using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BffServer.Controllers;

[AllowAnonymous]
[Route("auth")]
public class AuthController : Controller
{
    private readonly ILogger<AuthController> _logger;

    public AuthController(ILogger<AuthController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Custom login endpoint that redirects back to React app after authentication
    /// </summary>
    [HttpGet("login")]
    public IActionResult Login([FromQuery] string? returnUrl = null)
    {
        _logger.LogInformation("Custom login endpoint hit with returnUrl: {ReturnUrl}", returnUrl);

        // If user is already authenticated, redirect back to React
        if (User?.Identity?.IsAuthenticated == true)
        {
            _logger.LogInformation("User already authenticated, redirecting to React app");
            return Redirect("https://localhost:3000");
        }

        // Set return URL to React app
        var properties = new AuthenticationProperties
        {
            RedirectUri = "https://localhost:3000"
        };

        // Challenge with OIDC (redirect to Identity Server)
        return Challenge(properties, "oidc");
    }

    /// <summary>
    /// Custom logout endpoint that redirects back to React app after logout
    /// </summary>
    [HttpGet("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        _logger.LogInformation("Custom logout endpoint hit");

        var properties = new AuthenticationProperties
        {
            // This will be used as post_logout_redirect_uri
            RedirectUri = "https://localhost:3000"
        };

        // Sign out from cookie authentication
        await HttpContext.SignOutAsync("Cookies");

        // Sign out from OIDC (this will redirect to Identity Server logout page)
        return SignOut(properties, "oidc");
    }

    /// <summary>
    /// Callback after successful authentication
    /// This is called by OIDC middleware after returning from Identity Server
    /// </summary>
    [HttpGet("callback")]
    public IActionResult Callback()
    {
        _logger.LogInformation("Auth callback hit, redirecting to React app");

        // Redirect to React app
        return Redirect("https://localhost:3000");
    }
}