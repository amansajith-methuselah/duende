using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace BffServer.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AdminController> _logger;

        public AdminController(IConfiguration configuration, ILogger<AdminController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        // GET: api/admin/users
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers([FromQuery] string? tenantId = null)
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                var users = new List<object>();

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    var query = @"
                        SELECT 
                            u.Id,
                            u.UserName,
                            u.Email,
                            u.PhoneNumber,
                            u.EmailConfirmed,
                            u.TenantId,
                            u.LockoutEnd,
                            STRING_AGG(r.Name, ',') AS Roles
                        FROM AspNetUsers u
                        LEFT JOIN AspNetUserRoles ur ON u.Id = ur.UserId
                        LEFT JOIN AspNetRoles r ON ur.RoleId = r.Id
                        WHERE (@TenantId IS NULL OR u.TenantId = @TenantId)
                        GROUP BY u.Id, u.UserName, u.Email, u.PhoneNumber, u.EmailConfirmed, u.TenantId, u.LockoutEnd
                        ORDER BY u.TenantId, u.UserName";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@TenantId", (object?)tenantId ?? DBNull.Value);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                users.Add(new
                                {
                                    id = reader["Id"].ToString(),
                                    userName = reader["UserName"].ToString(),
                                    email = reader["Email"].ToString(),
                                    phoneNumber = reader["PhoneNumber"]?.ToString(),
                                    emailConfirmed = Convert.ToBoolean(reader["EmailConfirmed"]),
                                    tenantId = reader["TenantId"].ToString(),
                                    isLockedOut = reader["LockoutEnd"] != DBNull.Value &&
                                                  Convert.ToDateTime(reader["LockoutEnd"]) > DateTime.UtcNow,
                                    roles = reader["Roles"]?.ToString()?.Split(',') ?? Array.Empty<string>()
                                });
                            }
                        }
                    }
                }

                return Ok(new { success = true, users });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching users");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // GET: api/admin/tenants
        [HttpGet("tenants")]
        public async Task<IActionResult> GetAllTenants()
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                var tenants = new List<object>();

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    var query = @"
                        SELECT 
                            t.TenantId,
                            t.Name,
                            t.Domain,
                            t.IsActive,
                            COUNT(u.Id) AS UserCount
                        FROM Tenants t
                        LEFT JOIN AspNetUsers u ON t.TenantId = u.TenantId
                        GROUP BY t.TenantId, t.Name, t.Domain, t.IsActive
                        ORDER BY t.Name";

                    using (var command = new SqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                tenants.Add(new
                                {
                                    tenantId = reader["TenantId"].ToString(),
                                    name = reader["Name"].ToString(),
                                    domain = reader["Domain"].ToString(),
                                    isActive = Convert.ToBoolean(reader["IsActive"]),
                                    userCount = Convert.ToInt32(reader["UserCount"])
                                });
                            }
                        }
                    }
                }

                return Ok(new { success = true, tenants });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching tenants");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // POST: api/admin/users/{userId}/lock
        [HttpPost("users/{userId}/lock")]
        public async Task<IActionResult> LockUser(string userId, [FromBody] LockUserRequest request)
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    var lockoutEnd = request.Lock ? DateTime.UtcNow.AddYears(100) : (DateTime?)null;

                    var query = @"
                        UPDATE AspNetUsers 
                        SET LockoutEnd = @LockoutEnd, LockoutEnabled = 1
                        WHERE Id = @UserId";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", userId);
                        command.Parameters.AddWithValue("@LockoutEnd", (object?)lockoutEnd ?? DBNull.Value);

                        var rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected == 0)
                        {
                            return NotFound(new { success = false, error = "User not found" });
                        }
                    }
                }

                return Ok(new { success = true, message = request.Lock ? "User locked" : "User unlocked" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error locking/unlocking user");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
    }

    public class LockUserRequest
    {
        public bool Lock { get; set; }
    }
}