using BffServer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BffServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UserController : ControllerBase
{
    private readonly ILogger<UserController> _logger;

    public UserController(ILogger<UserController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get current user information
    /// This endpoint is called by the React app to check authentication status
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult GetUser()
    {
        if (User?.Identity?.IsAuthenticated == true)
        {
            var claims = User.Claims
                .Select(c => new { c.Type, c.Value })
                .ToDictionary(c => c.Type, c => c.Value);

            var userInfo = new UserViewModel
            {
                IsAuthenticated = true,
                Username = User.FindFirst("preferred_username")?.Value
                    ?? User.FindFirst("name")?.Value
                    ?? User.Identity.Name,
                Email = User.FindFirst("email")?.Value,
                Claims = claims
            };

            _logger.LogInformation("User info retrieved for: {Username}", userInfo.Username);
            return Ok(userInfo);
        }

        _logger.LogInformation("User not authenticated");
        return Ok(new UserViewModel { IsAuthenticated = false });
    }

    /// <summary>
    /// Get user profile with all claims
    /// Requires authentication
    /// </summary>
    [HttpGet("profile")]
    [Authorize]
    public IActionResult GetProfile()
    {
        var claims = User.Claims
            .Select(c => new { c.Type, c.Value })
            .ToList();

        _logger.LogInformation("Profile retrieved for user: {Username}", User.Identity?.Name);

        return Ok(new
        {
            isAuthenticated = true,
            username = User.FindFirst("preferred_username")?.Value ?? User.Identity?.Name,
            email = User.FindFirst("email")?.Value,
            name = User.FindFirst("name")?.Value,
            givenName = User.FindFirst("given_name")?.Value,
            familyName = User.FindFirst("family_name")?.Value,
            phoneNumber = User.FindFirst("phone_number")?.Value,
            birthdate = User.FindFirst("birthdate")?.Value,
            country = User.FindFirst("country")?.Value,
            claims = claims
        });
    }

    /// <summary>
    /// Get user claims as a simple list
    /// </summary>
    [HttpGet("claims")]
    [Authorize]
    public IActionResult GetClaims()
    {
        var claims = User.Claims
            .Select(c => new
            {
                type = c.Type,
                value = c.Value
            })
            .ToList();

        return Ok(claims);
    }
}