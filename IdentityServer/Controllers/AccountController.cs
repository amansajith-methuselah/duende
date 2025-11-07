using Duende.IdentityServer.Events;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Services;
using IdentityServer.Data;
using IdentityServer.Models.AccountViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace IdentityServer.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IIdentityServerInteractionService _interaction;
    private readonly IEventService _events;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IIdentityServerInteractionService interaction,
        IEventService events,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _interaction = interaction;
        _events = events;
        _logger = logger;
    }

    #region Login

    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        _logger.LogInformation("Login page accessed with ReturnUrl: {ReturnUrl}", returnUrl);

        if (User?.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(returnUrl))
        {
            var context = await _interaction.GetAuthorizationContextAsync(returnUrl);
            if (context != null)
            {
                _logger.LogInformation("User already authenticated, continuing OAuth flow");
                return Redirect(returnUrl);
            }
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        ViewData["ReturnUrl"] = model.ReturnUrl;

        _logger.LogInformation("Login attempt for user: {Username} with ReturnUrl: {ReturnUrl}",
            model.Username, model.ReturnUrl);

        if (ModelState.IsValid)
        {
            var result = await _signInManager.PasswordSignInAsync(
                model.Username,
                model.Password,
                model.RememberLogin,
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                var user = await _userManager.FindByNameAsync(model.Username);

                await _events.RaiseAsync(new UserLoginSuccessEvent(
                    user.UserName,
                    user.Id,
                    user.UserName,
                    clientId: null));

                _logger.LogInformation("User {Username} logged in successfully", model.Username);

                if (!string.IsNullOrEmpty(model.ReturnUrl))
                {
                    var context = await _interaction.GetAuthorizationContextAsync(model.ReturnUrl);

                    if (context != null)
                    {
                        _logger.LogInformation("OAuth context found. Client: {ClientId}. Redirecting to: {ReturnUrl}",
                            context.Client?.ClientId, model.ReturnUrl);

                        return Redirect(model.ReturnUrl);
                    }

                    if (Url.IsLocalUrl(model.ReturnUrl))
                    {
                        _logger.LogInformation("Local return URL. Redirecting to: {ReturnUrl}", model.ReturnUrl);
                        return Redirect(model.ReturnUrl);
                    }

                    _logger.LogWarning("Return URL is not valid: {ReturnUrl}", model.ReturnUrl);
                }

                _logger.LogInformation("No valid return URL. Redirecting to home page.");
                return RedirectToAction(nameof(HomeController.Index), "Home");
            }

            if (result.RequiresTwoFactor)
            {
                return RedirectToAction(nameof(LoginWith2fa), new { model.ReturnUrl, model.RememberLogin });
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("User account locked out.");
                return RedirectToAction(nameof(Lockout));
            }

            await _events.RaiseAsync(new UserLoginFailureEvent(model.Username, "invalid credentials"));
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        }

        return View(model);
    }

    #endregion

    #region Register

    [HttpGet]
    public IActionResult Register(string? returnUrl = null)
    {
        _logger.LogInformation("Registration page loaded with ReturnUrl: {ReturnUrl}", returnUrl);

        ViewData["ReturnUrl"] = returnUrl;
        return View(new RegisterViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        ViewData["ReturnUrl"] = model.ReturnUrl;

        _logger.LogInformation("Registration submitted with ReturnUrl: {ReturnUrl}", model.ReturnUrl);

        if (ModelState.IsValid)
        {
            var user = new ApplicationUser
            {
                UserName = model.Username,
                Email = model.Email,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                _logger.LogInformation("User created a new account with password.");

                var claims = new List<Claim>
                {
                    new Claim("name", $"{model.FirstName} {model.LastName}"),
                    new Claim("given_name", model.FirstName),
                    new Claim("family_name", model.LastName),
                    new Claim("email", model.Email),
                    new Claim("preferred_username", model.Username),
                    new Claim("email_verified", "true")
                };

                if (!string.IsNullOrEmpty(model.PhoneNumber))
                {
                    claims.Add(new Claim("phone_number", model.PhoneNumber));
                    claims.Add(new Claim("phone_number_verified", "true"));
                    user.PhoneNumber = model.PhoneNumber;
                    user.PhoneNumberConfirmed = true;
                }

                if (model.DateOfBirth.HasValue)
                {
                    claims.Add(new Claim("birthdate", model.DateOfBirth.Value.ToString("yyyy-MM-dd")));
                }

                if (!string.IsNullOrEmpty(model.Country))
                {
                    claims.Add(new Claim("country", model.Country));
                }

                await _userManager.AddClaimsAsync(user, claims);
                await _userManager.UpdateAsync(user);

                _logger.LogInformation("User claims added successfully.");

                await _signInManager.SignInAsync(user, isPersistent: false);

                _logger.LogInformation("User signed in successfully.");

                await _events.RaiseAsync(new UserLoginSuccessEvent(
                    user.UserName,
                    user.Id,
                    user.UserName,
                    clientId: null));

                if (!string.IsNullOrEmpty(model.ReturnUrl))
                {
                    var context = await _interaction.GetAuthorizationContextAsync(model.ReturnUrl);

                    if (context != null)
                    {
                        _logger.LogInformation("OAuth context found. Client: {ClientId}. Redirecting to: {ReturnUrl}",
                            context.Client?.ClientId, model.ReturnUrl);

                        return Redirect(model.ReturnUrl);
                    }
                    else if (Url.IsLocalUrl(model.ReturnUrl))
                    {
                        _logger.LogInformation("Local return URL found. Redirecting to: {ReturnUrl}", model.ReturnUrl);
                        return Redirect(model.ReturnUrl);
                    }
                    else
                    {
                        _logger.LogWarning("Return URL is not local: {ReturnUrl}", model.ReturnUrl);
                    }
                }

                _logger.LogInformation("No OAuth context. Showing success page.");
                return RedirectToAction(nameof(RegisterSuccess), new { returnUrl = model.ReturnUrl });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult RegisterSuccess(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    #endregion

    #region Logout

    [HttpGet]
    public async Task<IActionResult> Logout(string? logoutId)
    {
        _logger.LogInformation("Logout GET initiated with logoutId: {LogoutId}", logoutId);

        // Get the logout context
        var context = await _interaction.GetLogoutContextAsync(logoutId);

        // If user is authenticated, show logout confirmation
        if (User?.Identity?.IsAuthenticated == true)
        {
            ViewData["LogoutId"] = logoutId;
            ViewData["ClientName"] = context?.ClientName ?? "Application";
            ViewData["PostLogoutRedirectUri"] = context?.PostLogoutRedirectUri;
            return View();
        }

        // User not authenticated, perform logout anyway
        return await PerformLogout(logoutId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(string? logoutId, string? returnUrl)
    {
        _logger.LogInformation("Logout POST initiated with logoutId: {LogoutId}", logoutId);
        return await PerformLogout(logoutId);
    }

    private async Task<IActionResult> PerformLogout(string? logoutId)
    {
        var context = await _interaction.GetLogoutContextAsync(logoutId);

        if (User?.Identity?.IsAuthenticated == true)
        {
            var subjectId = User.GetSubjectId();
            var displayName = User.GetDisplayName();

            // Sign out from Identity
            await _signInManager.SignOutAsync();

            // Raise logout event
            await _events.RaiseAsync(new UserLogoutSuccessEvent(subjectId, displayName));

            _logger.LogInformation("User {DisplayName} logged out successfully", displayName);
        }

        // Get the post logout redirect URI
        var postLogoutUri = context?.PostLogoutRedirectUri;

        if (!string.IsNullOrEmpty(postLogoutUri))
        {
            _logger.LogInformation("Redirecting to post logout URI: {PostLogoutUri}", postLogoutUri);
            return Redirect(postLogoutUri);
        }

        // Default redirect
        _logger.LogInformation("No post logout URI, redirecting to home");
        return RedirectToAction("Index", "Home");
    }

    #endregion

    #region Helpers

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Lockout()
    {
        return View();
    }

    [HttpGet]
    public IActionResult LoginWith2fa(string returnUrl, bool rememberMe)
    {
        return View();
    }

    #endregion
}