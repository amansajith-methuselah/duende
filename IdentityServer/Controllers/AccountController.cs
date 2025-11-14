using Duende.IdentityServer.Events;
using Microsoft.EntityFrameworkCore;
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

        // Get tenant from HttpContext (set by middleware)
        var tenant = HttpContext.Items["Tenant"] as Tenant;

        ViewData["ReturnUrl"] = returnUrl;
        ViewData["TenantName"] = tenant?.Name ?? "Identity Server";
        ViewData["TenantColor"] = tenant?.PrimaryColor ?? "#1E40AF";
        ViewData["TenantLogo"] = tenant?.LogoUrl;

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
            // CRITICAL: Get current tenant from HttpContext
            var tenantId = HttpContext.Items["TenantId"] as string;

            // CRITICAL: Find user by username AND tenant
            var user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.UserName == model.Username && u.TenantId == tenantId);

            if (user == null)
            {
                // User doesn't exist in this tenant
                _logger.LogWarning("Login failed: User {Username} not found in tenant {TenantId}", model.Username, tenantId);
                await _events.RaiseAsync(new UserLoginFailureEvent(model.Username, "invalid credentials"));
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return View(model);
            }

            // Now attempt sign in with the tenant-specific user
            var result = await _signInManager.PasswordSignInAsync(
                user,
                model.Password,
                model.RememberLogin,
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                await _events.RaiseAsync(new UserLoginSuccessEvent(
                    user.UserName,
                    user.Id,
                    user.UserName,
                    clientId: null));

                _logger.LogInformation("User {Username} logged in successfully to tenant {TenantId}", model.Username, tenantId);

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

        // Get tenant from HttpContext (set by middleware)
        var tenant = HttpContext.Items["Tenant"] as Tenant;

        ViewData["ReturnUrl"] = returnUrl;
        ViewData["TenantName"] = tenant?.Name ?? "Identity Server";
        ViewData["TenantColor"] = tenant?.PrimaryColor ?? "#1E40AF";
        ViewData["TenantLogo"] = tenant?.LogoUrl;

        return View(new RegisterViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var tenantId = HttpContext.Items["TenantId"]?.ToString();
        if (string.IsNullOrEmpty(tenantId))
        {
            ModelState.AddModelError(string.Empty, "Tenant not found");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email, // Use Email as username for now
            Email = model.Email,
            PhoneNumber = model.PhoneNumber,
            EmailConfirmed = true,
            TenantId = tenantId
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            // Add custom claims
            var claims = new List<System.Security.Claims.Claim>
        {
            new System.Security.Claims.Claim("name", $"{model.FirstName} {model.LastName}"),
            new System.Security.Claims.Claim("given_name", model.FirstName),
            new System.Security.Claims.Claim("family_name", model.LastName)
        };

            if (!string.IsNullOrEmpty(model.PhoneNumber))
            {
                claims.Add(new System.Security.Claims.Claim("phone_number", model.PhoneNumber));
            }

            if (model.DateOfBirth.HasValue)
            {
                claims.Add(new System.Security.Claims.Claim("birthdate", model.DateOfBirth.Value.ToString("yyyy-MM-dd")));
            }

            if (!string.IsNullOrEmpty(model.Country))
            {
                claims.Add(new System.Security.Claims.Claim("country", model.Country));
            }

            await _userManager.AddClaimsAsync(user, claims);

            _logger.LogInformation("User {Email} created successfully in tenant {TenantId}", model.Email, tenantId);

            return RedirectToAction(nameof(Login));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
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

        // Get tenant info for branding
        var tenant = HttpContext.Items["Tenant"] as Tenant;

        ViewData["TenantName"] = tenant?.Name ?? "Identity Server";
        ViewData["TenantColor"] = tenant?.PrimaryColor ?? "#1E40AF";
        ViewData["TenantLogo"] = tenant?.LogoUrl;

        // If we have a valid logout context (meaning a client initiated this), show confirmation
        if (context != null && !string.IsNullOrEmpty(context.ClientId))
        {
            ViewData["LogoutId"] = logoutId;
            ViewData["ClientName"] = context.ClientName ?? "Application";
            ViewData["PostLogoutRedirectUri"] = context.PostLogoutRedirectUri;

            // Show logout confirmation page
            return View();
        }

        // No valid context, perform logout anyway
        return await PerformLogout(logoutId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(LogoutInputModel model)
    {
        _logger.LogInformation("=== LOGOUT POST STARTED ===");
        _logger.LogInformation("LogoutId from model: {LogoutId}", model.LogoutId ?? "NULL");
        _logger.LogInformation("Form values:");

        foreach (var key in Request.Form.Keys)
        {
            _logger.LogInformation("  {Key} = {Value}", key, Request.Form[key]);
        }

        return await PerformLogout(model.LogoutId);
    }

    private async Task<IActionResult> PerformLogout(string? logoutId)
    {
        _logger.LogInformation("=== PERFORM LOGOUT STARTED ===");
        _logger.LogInformation("LogoutId parameter: {LogoutId}", logoutId ?? "NULL");

        var context = await _interaction.GetLogoutContextAsync(logoutId);

        _logger.LogInformation("Context is NULL: {IsNull}", context == null);

        if (context != null)
        {
            _logger.LogInformation("Context ClientId: {ClientId}", context.ClientId ?? "NULL");
            _logger.LogInformation("Context PostLogoutRedirectUri: {Uri}", context.PostLogoutRedirectUri ?? "NULL");
            _logger.LogInformation("Context SubjectId: {SubjectId}", context.SubjectId ?? "NULL");
        }

        // Try to get subject ID safely
        string? subjectId = null;
        try
        {
            subjectId = User?.Identity?.IsAuthenticated == true ? User.GetSubjectId() : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not get SubjectId from User");
        }

        // Fallback to context SubjectId
        subjectId ??= context?.SubjectId;

        if (!string.IsNullOrEmpty(subjectId))
        {
            string? displayName = null;
            try
            {
                displayName = User?.Identity?.IsAuthenticated == true ? User.GetDisplayName() : null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not get DisplayName from User");
            }

            displayName ??= "Unknown User";

            _logger.LogInformation("Signing out user: {DisplayName} ({SubjectId})", displayName, subjectId);

            // Sign out from ASP.NET Core Identity (if authenticated locally)
            if (User?.Identity?.IsAuthenticated == true)
            {
                await _signInManager.SignOutAsync();
                _logger.LogInformation("User signed out from local authentication");
            }

            // Revoke user consent for this client (if clientId is available)
            if (!string.IsNullOrEmpty(context?.ClientId))
            {
                try
                {
                    await _interaction.RevokeUserConsentAsync(context.ClientId);
                    _logger.LogInformation("Revoked user consent for client: {ClientId}", context.ClientId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not revoke consent");
                }
            }

            // Raise logout event
            await _events.RaiseAsync(new UserLogoutSuccessEvent(subjectId, displayName));

            _logger.LogInformation("User {DisplayName} logged out successfully", displayName);
        }
        else
        {
            _logger.LogInformation("No user session to logout - performing anonymous logout");
        }

        // Get the post logout redirect URI
        var postLogoutUri = context?.PostLogoutRedirectUri;

        _logger.LogInformation("PostLogoutUri: {Uri}", postLogoutUri ?? "NULL");

        // If no PostLogoutRedirectUri from context, construct tenant-specific React URL
        if (string.IsNullOrEmpty(postLogoutUri))
        {
            var host = HttpContext.Request.Host.Value; // e.g., "tenant1.localhost:7140"

            // Replace IdentityServer port with React port
            var reactHost = host.Replace(":7140", ":3000");
            postLogoutUri = $"https://{reactHost}";

            _logger.LogInformation("No PostLogoutRedirectUri in context, using fallback: {Uri}", postLogoutUri);
        }

        _logger.LogInformation("Redirecting to: {PostLogoutUri}", postLogoutUri);
        return Redirect(postLogoutUri);
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

    public class LogoutInputModel
    {
        public string? LogoutId { get; set; }
    }

    #endregion
}