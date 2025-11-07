using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BffServer.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly ILogger<AccountController> _logger;

    public AccountController(ILogger<AccountController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        _logger.LogInformation("Login initiated with returnUrl: {ReturnUrl}", returnUrl);

        if (User?.Identity?.IsAuthenticated == true)
        {
            _logger.LogInformation("User already authenticated");

            // If returnUrl is provided and is local, redirect there
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            // Otherwise go to home
            return RedirectToAction("Index", "Home");
        }

        // Store the original returnUrl in session or pass it through
        if (!string.IsNullOrEmpty(returnUrl))
        {
            HttpContext.Session.SetString("ReturnUrl", returnUrl);
        }

        // Redirect to BFF login endpoint
        // Don't pass external URLs to BFF
        return Redirect("/bff/login");
    }

    [HttpGet]
    [Authorize]
    public IActionResult Logout(string? returnUrl = null)
    {
        _logger.LogInformation("Logout initiated with returnUrl: {ReturnUrl}", returnUrl);

        // Redirect to BFF logout endpoint
        return Redirect("/bff/logout");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        _logger.LogWarning("Access denied for user: {User}",
            User?.Identity?.Name ?? "Anonymous");
        return View();
    }

    [HttpGet]
    [Authorize]
    public IActionResult Profile()
    {
        return View();
    }
}