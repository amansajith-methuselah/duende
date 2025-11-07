using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;

namespace BffServer.Controllers;

/// <summary>
/// This controller demonstrates how to proxy API calls to your backend
/// The BFF pattern protects your backend APIs by proxying through this server
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ApiController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiController> _logger;

    public ApiController(
        IHttpClientFactory httpClientFactory,
        ILogger<ApiController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Example: Get data from backend API
    /// This would call your actual backend API at https://localhost:6001/api/data
    /// </summary>
    [HttpGet("data")]
    public async Task<IActionResult> GetData()
    {
        try
        {
            // Get the access token from the authenticated user
            var accessToken = await HttpContext.GetTokenAsync("access_token");

            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("No access token available for user: {Username}",
                    User.Identity?.Name);
                return Unauthorized(new { error = "No access token available" });
            }

            // Create HTTP client to call backend API
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            // Call your backend API
            var response = await client.GetAsync("https://localhost:6001/api/data");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Successfully retrieved data from backend API");
                return Content(content, "application/json");
            }

            _logger.LogWarning("Backend API returned status code: {StatusCode}",
                response.StatusCode);
            return StatusCode((int)response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error calling backend API");
            return StatusCode(500, new { error = "Error calling backend API", message = ex.Message });
        }
    }

    /// <summary>
    /// Example: Post data to backend API
    /// </summary>
    [HttpPost("data")]
    public async Task<IActionResult> PostData([FromBody] object data)
    {
        try
        {
            var accessToken = await HttpContext.GetTokenAsync("access_token");

            if (string.IsNullOrEmpty(accessToken))
            {
                return Unauthorized(new { error = "No access token available" });
            }

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.PostAsJsonAsync(
                "https://localhost:6001/api/data",
                data);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }

            return StatusCode((int)response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error calling backend API");
            return StatusCode(500, new { error = "Error calling backend API", message = ex.Message });
        }
    }

    /// <summary>
    /// Simple test endpoint to verify BFF API is working
    /// </summary>
    [HttpGet("test")]
    public IActionResult Test()
    {
        return Ok(new
        {
            message = "BFF API is working!",
            user = User.Identity?.Name,
            timestamp = DateTime.UtcNow
        });
    }
}