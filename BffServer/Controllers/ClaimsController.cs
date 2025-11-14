using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.Metrics;
using System.Security.Claims;

namespace BffServer.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ClaimsController : ControllerBase
    {
        private readonly ILogger<ClaimsController> _logger;

        public ClaimsController(ILogger<ClaimsController> logger)
        {
            _logger = logger;
        }

        // GET: api/claims
        // Returns all user claims
        [HttpGet]
        public IActionResult GetAllClaims()
        {
            try
            {
                if (User?.Identity?.IsAuthenticated != true)
                {
                    return Unauthorized(new { success = false, error = "User not authenticated" });
                }

                var claims = User.Claims.Select(c => new
                {
                    type = c.Type,
                    value = c.Value
                }).ToList();

                return Ok(new
                {
                    success = true,
                    claims,
                    count = claims.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all claims");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // GET: api/claims/{claimType}
        // Returns a specific claim by type (e.g., "email", "name", "tenant_id", "role")
        [HttpGet("{claimType}")]
        public IActionResult GetClaimByType(string claimType)
        {
            try
            {
                if (User?.Identity?.IsAuthenticated != true)
                {
                    return Unauthorized(new { success = false, error = "User not authenticated" });
                }

                // Find all claims with the specified type
                var claims = User.Claims
                    .Where(c => c.Type.Equals(claimType, StringComparison.OrdinalIgnoreCase))
                    .Select(c => c.Value)
                    .ToList();

                if (!claims.Any())
                {
                    return NotFound(new
                    {
                        success = false,
                        error = $"Claim type '{claimType}' not found",
                        message = "User does not have this claim"
                    });
                }

                return Ok(new
                {
                    success = true,
                    claimType,
                    values = claims,
                    count = claims.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving claim: {ClaimType}", claimType);
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // GET: api/claims/search?query=email
        // Search for claims containing the query string (in type or value)
        [HttpGet("search")]
        public IActionResult SearchClaims([FromQuery] string query)
        {
            try
            {
                if (User?.Identity?.IsAuthenticated != true)
                {
                    return Unauthorized(new { success = false, error = "User not authenticated" });
                }

                if (string.IsNullOrWhiteSpace(query))
                {
                    return BadRequest(new { success = false, error = "Query parameter is required" });
                }

                var matchingClaims = User.Claims
                    .Where(c =>
                        c.Type.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        c.Value.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .Select(c => new
                    {
                        type = c.Type,
                        value = c.Value
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    query,
                    claims = matchingClaims,
                    count = matchingClaims.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching claims with query: {Query}", query);
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // GET: api/claims/user-info
        // Returns a formatted user information object
        [HttpGet("user-info")]
        public IActionResult GetUserInfo()
        {
            try
            {
                if (User?.Identity?.IsAuthenticated != true)
                {
                    return Unauthorized(new { success = false, error = "User not authenticated" });
                }

                var userInfo = new
                {
                    userId = User.FindFirst("sub")?.Value,
                    username = User.FindFirst("preferred_username")?.Value ?? User.Identity.Name,
                    name = User.FindFirst("name")?.Value,
                    givenName = User.FindFirst("given_name")?.Value,
                    familyName = User.FindFirst("family_name")?.Value,
                    email = User.FindFirst("email")?.Value,
                    emailVerified = bool.TryParse(User.FindFirst("email_verified")?.Value, out var ev) && ev,
                    phoneNumber = User.FindFirst("phone_number")?.Value,
                    birthdate = User.FindFirst("birthdate")?.Value,
                    country = User.FindFirst("country")?.Value,
                    tenantId = User.FindFirst("tenant_id")?.Value,
                    roles = User.FindAll("role").Select(c => c.Value).ToList(),
                    isAdmin = User.IsInRole("Admin")
                };

                return Ok(new
                {
                    success = true,
                    user = userInfo
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user info");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
    }
}