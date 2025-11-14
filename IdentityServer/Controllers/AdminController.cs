using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IdentityServer.Data;

namespace IdentityServer.Controllers
{
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

        // POST: api/admin/migrate/{tenantId}
        [HttpPost("migrate/{tenantId}")]
        public async Task<IActionResult> RunMigrations(string tenantId)
        {
            try
            {
                var defaultConnectionString = _configuration.GetConnectionString("DefaultConnection");

                // Get tenant's custom connection string from database
                string? customConnectionString = null;
                bool useOwnDatabase = false;

                using (var connection = new Microsoft.Data.SqlClient.SqlConnection(defaultConnectionString))
                {
                    await connection.OpenAsync();

                    var query = "SELECT CustomConnectionString, UseOwnDatabase FROM Tenants WHERE TenantId = @TenantId";
                    using (var command = new Microsoft.Data.SqlClient.SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@TenantId", tenantId);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                useOwnDatabase = Convert.ToBoolean(reader["UseOwnDatabase"]);
                                customConnectionString = reader["CustomConnectionString"]?.ToString();
                            }
                            else
                            {
                                return NotFound(new { success = false, error = "Tenant not found" });
                            }
                        }
                    }
                }

                if (!useOwnDatabase)
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = "Tenant is not configured to use own database"
                    });
                }

                if (string.IsNullOrEmpty(customConnectionString))
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = "No custom connection string configured"
                    });
                }

                // Update migration status to InProgress
                await UpdateMigrationStatus(tenantId, "InProgress", null);

                // Run migrations using EF Core
                try
                {
                    var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
                    optionsBuilder.UseSqlServer(customConnectionString);

                    using var context = new ApplicationDbContext(optionsBuilder.Options);

                    _logger.LogInformation("Running migrations for tenant {TenantId}...", tenantId);
                    await context.Database.MigrateAsync();
                    _logger.LogInformation("✓ Migrations completed for tenant {TenantId}", tenantId);

                    // Update migration status to Success
                    await UpdateMigrationStatus(tenantId, "Success", DateTime.UtcNow);

                    return Ok(new
                    {
                        success = true,
                        message = "Migrations completed successfully",
                        completedAt = DateTime.UtcNow
                    });
                }
                catch (Exception migrationEx)
                {
                    _logger.LogError(migrationEx, "Migration failed for tenant {TenantId}", tenantId);
                    await UpdateMigrationStatus(tenantId, "Failed", null);

                    return StatusCode(500, new
                    {
                        success = false,
                        error = $"Migration failed: {migrationEx.Message}"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running migrations for tenant {TenantId}", tenantId);
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // Helper method to update migration status
        private async Task UpdateMigrationStatus(string tenantId, string status, DateTime? migrationDate)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
            await connection.OpenAsync();

            var query = @"
                UPDATE Tenants 
                SET DatabaseMigrationStatus = @Status,
                    LastMigrationDate = @MigrationDate
                WHERE TenantId = @TenantId";

            using var command = new Microsoft.Data.SqlClient.SqlCommand(query, connection);
            command.Parameters.AddWithValue("@TenantId", tenantId);
            command.Parameters.AddWithValue("@Status", status);
            command.Parameters.AddWithValue("@MigrationDate", (object?)migrationDate ?? DBNull.Value);

            await command.ExecuteNonQueryAsync();
        }
    }
}