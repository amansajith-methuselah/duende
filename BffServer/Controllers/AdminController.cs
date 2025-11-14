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
        private readonly IHttpClientFactory _httpClientFactory;

        public AdminController(
            IConfiguration configuration,
            ILogger<AdminController> logger,
            IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
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

        // DELETE: api/admin/users/{userId}
        [HttpDelete("users/{userId}")]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Delete user claims
                            await ExecuteNonQueryAsync(connection, transaction,
                                "DELETE FROM AspNetUserClaims WHERE UserId = @UserId", userId);

                            // Delete user roles
                            await ExecuteNonQueryAsync(connection, transaction,
                                "DELETE FROM AspNetUserRoles WHERE UserId = @UserId", userId);

                            // Delete user logins
                            await ExecuteNonQueryAsync(connection, transaction,
                                "DELETE FROM AspNetUserLogins WHERE UserId = @UserId", userId);

                            // Delete user tokens
                            await ExecuteNonQueryAsync(connection, transaction,
                                "DELETE FROM AspNetUserTokens WHERE UserId = @UserId", userId);

                            // Delete the user
                            var rowsAffected = await ExecuteNonQueryAsync(connection, transaction,
                                "DELETE FROM AspNetUsers WHERE Id = @UserId", userId);

                            if (rowsAffected == 0)
                            {
                                transaction.Rollback();
                                return NotFound(new { success = false, error = "User not found" });
                            }

                            transaction.Commit();
                            return Ok(new { success = true, message = "User deleted successfully" });
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // POST: api/admin/tenants
        [HttpPost("tenants")]
        public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.TenantId) ||
                    string.IsNullOrWhiteSpace(request.Name) ||
                    string.IsNullOrWhiteSpace(request.Domain))
                {
                    return BadRequest(new { success = false, error = "TenantId, Name, and Domain are required" });
                }

                var connectionString = _configuration.GetConnectionString("DefaultConnection");

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Check if tenant already exists
                    var checkQuery = "SELECT COUNT(*) FROM Tenants WHERE TenantId = @TenantId";
                    using (var checkCommand = new SqlCommand(checkQuery, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@TenantId", request.TenantId);
                        var exists = (int)await checkCommand.ExecuteScalarAsync() > 0;

                        if (exists)
                        {
                            return Conflict(new { success = false, error = "Tenant with this ID already exists" });
                        }
                    }

                    // Insert new tenant
                    var insertQuery = @"
                        INSERT INTO Tenants (TenantId, Name, Domain, IsActive, PrimaryColor, CreatedAt, UpdatedAt)
                        VALUES (@TenantId, @Name, @Domain, 1, @PrimaryColor, GETUTCDATE(), GETUTCDATE())";

                    using (var insertCommand = new SqlCommand(insertQuery, connection))
                    {
                        insertCommand.Parameters.AddWithValue("@TenantId", request.TenantId);
                        insertCommand.Parameters.AddWithValue("@Name", request.Name);
                        insertCommand.Parameters.AddWithValue("@Domain", request.Domain);
                        insertCommand.Parameters.AddWithValue("@PrimaryColor", request.PrimaryColor ?? "#059669");

                        await insertCommand.ExecuteNonQueryAsync();
                    }

                    // Create BFF client for the new tenant
                    await CreateTenantClient(connection, request.TenantId);
                }

                return Ok(new { success = true, message = "Tenant created successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating tenant");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // DELETE: api/admin/tenants/{tenantId}
        [HttpDelete("tenants/{tenantId}")]
        public async Task<IActionResult> DeleteTenant(string tenantId)
        {
            try
            {
                // Prevent deleting admin tenant
                if (tenantId.Equals("admin", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { success = false, error = "Cannot delete the admin tenant" });
                }

                var connectionString = _configuration.GetConnectionString("DefaultConnection");

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Check if tenant has users
                    var checkUsersQuery = "SELECT COUNT(*) FROM AspNetUsers WHERE TenantId = @TenantId";
                    using (var checkCommand = new SqlCommand(checkUsersQuery, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@TenantId", tenantId);
                        var userCount = (int)await checkCommand.ExecuteScalarAsync();

                        if (userCount > 0)
                        {
                            return BadRequest(new
                            {
                                success = false,
                                error = $"Cannot delete tenant with {userCount} user(s). Delete users first."
                            });
                        }
                    }

                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Get client ID
                            var getClientIdQuery = "SELECT Id FROM Clients WHERE TenantId = @TenantId";
                            int? clientId = null;

                            using (var getClientCommand = new SqlCommand(getClientIdQuery, connection, transaction))
                            {
                                getClientCommand.Parameters.AddWithValue("@TenantId", tenantId);
                                var result = await getClientCommand.ExecuteScalarAsync();
                                if (result != null && result != DBNull.Value)
                                {
                                    clientId = Convert.ToInt32(result);
                                }
                            }

                            // Delete client-related data if client exists
                            if (clientId.HasValue)
                            {
                                await ExecuteNonQueryAsync(connection, transaction,
                                    "DELETE FROM ClientGrantTypes WHERE ClientId = @ClientId", clientId.Value.ToString());
                                await ExecuteNonQueryAsync(connection, transaction,
                                    "DELETE FROM ClientScopes WHERE ClientId = @ClientId", clientId.Value.ToString());
                                await ExecuteNonQueryAsync(connection, transaction,
                                    "DELETE FROM ClientRedirectUris WHERE ClientId = @ClientId", clientId.Value.ToString());
                                await ExecuteNonQueryAsync(connection, transaction,
                                    "DELETE FROM ClientPostLogoutRedirectUris WHERE ClientId = @ClientId", clientId.Value.ToString());
                                await ExecuteNonQueryAsync(connection, transaction,
                                    "DELETE FROM ClientCorsOrigins WHERE ClientId = @ClientId", clientId.Value.ToString());
                                await ExecuteNonQueryAsync(connection, transaction,
                                    "DELETE FROM Clients WHERE Id = @ClientId", clientId.Value.ToString());
                            }

                            // Delete tenant
                            var rowsAffected = await ExecuteNonQueryAsync(connection, transaction,
                                "DELETE FROM Tenants WHERE TenantId = @TenantId", tenantId);

                            if (rowsAffected == 0)
                            {
                                transaction.Rollback();
                                return NotFound(new { success = false, error = "Tenant not found" });
                            }

                            transaction.Commit();
                            return Ok(new { success = true, message = "Tenant deleted successfully" });
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting tenant");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // Helper method to execute non-query commands
        private async Task<int> ExecuteNonQueryAsync(SqlConnection connection, SqlTransaction transaction, string query, string paramValue)
        {
            using (var command = new SqlCommand(query, connection, transaction))
            {
                // Determine parameter name from query
                var paramName = query.Contains("@UserId") ? "@UserId" :
                               query.Contains("@ClientId") ? "@ClientId" :
                               "@TenantId";
                command.Parameters.AddWithValue(paramName, paramValue);
                return await command.ExecuteNonQueryAsync();
            }
        }

        // Helper method to create client for new tenant
        private async Task CreateTenantClient(SqlConnection connection, string tenantId)
        {
            // Copy from an existing tenant client (tenant1)
            var copyClientQuery = @"
                INSERT INTO Clients (
                    ClientId, ClientName, Enabled, ProtocolType, RequireClientSecret, RequireConsent,
                    AllowRememberConsent, AlwaysIncludeUserClaimsInIdToken, RequirePkce, AllowPlainTextPkce,
                    RequireRequestObject, AllowAccessTokensViaBrowser, RequireDPoP, DPoPValidationMode,
                    DPoPClockSkew, FrontChannelLogoutSessionRequired, BackChannelLogoutSessionRequired,
                    AllowOfflineAccess, IdentityTokenLifetime, AllowedIdentityTokenSigningAlgorithms,
                    AccessTokenLifetime, AuthorizationCodeLifetime, ConsentLifetime, AbsoluteRefreshTokenLifetime,
                    SlidingRefreshTokenLifetime, RefreshTokenUsage, UpdateAccessTokenClaimsOnRefresh,
                    RefreshTokenExpiration, AccessTokenType, EnableLocalLogin, IncludeJwtId,
                    AlwaysSendClientClaims, PushedAuthorizationLifetime, RequirePushedAuthorization,
                    TenantId, DeviceCodeLifetime, CibaLifetime, PollingInterval, CoordinateLifetimeWithUserSession,
                    Description, ClientUri, LogoUri, ClientClaimsPrefix, PairWiseSubjectSalt, InitiateLoginUri,
                    UserSsoLifetime, UserCodeType, NonEditable, Created, Updated, LastAccessed
                )
                SELECT 
                    @NewClientId, @NewClientName, Enabled, ProtocolType, RequireClientSecret, RequireConsent,
                    AllowRememberConsent, AlwaysIncludeUserClaimsInIdToken, RequirePkce, AllowPlainTextPkce,
                    RequireRequestObject, AllowAccessTokensViaBrowser, RequireDPoP, DPoPValidationMode,
                    DPoPClockSkew, FrontChannelLogoutSessionRequired, BackChannelLogoutSessionRequired,
                    AllowOfflineAccess, IdentityTokenLifetime, AllowedIdentityTokenSigningAlgorithms,
                    AccessTokenLifetime, AuthorizationCodeLifetime, ConsentLifetime, AbsoluteRefreshTokenLifetime,
                    SlidingRefreshTokenLifetime, RefreshTokenUsage, UpdateAccessTokenClaimsOnRefresh,
                    RefreshTokenExpiration, AccessTokenType, EnableLocalLogin, IncludeJwtId,
                    AlwaysSendClientClaims, PushedAuthorizationLifetime, RequirePushedAuthorization,
                    @TenantId, DeviceCodeLifetime, CibaLifetime, PollingInterval, CoordinateLifetimeWithUserSession,
                    Description, ClientUri, LogoUri, ClientClaimsPrefix, PairWiseSubjectSalt, InitiateLoginUri,
                    UserSsoLifetime, UserCodeType, NonEditable, GETUTCDATE(), GETUTCDATE(), NULL
                FROM Clients 
                WHERE ClientId = 'bff-client-tenant1';

                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using (var command = new SqlCommand(copyClientQuery, connection))
            {
                command.Parameters.AddWithValue("@NewClientId", $"bff-client-{tenantId}");
                command.Parameters.AddWithValue("@NewClientName", $"BFF Client for {tenantId}");
                command.Parameters.AddWithValue("@TenantId", tenantId);

                var newClientId = (int)await command.ExecuteScalarAsync();

                // Copy grant types, scopes, etc.
                await CopyClientConfiguration(connection, newClientId, tenantId);
            }
        }

        private async Task CopyClientConfiguration(SqlConnection connection, int newClientId, string tenantId)
        {
            var tenant1ClientId = await GetClientId(connection, "bff-client-tenant1");

            // Copy grant types
            await ExecuteCopyQuery(connection,
                "INSERT INTO ClientGrantTypes (ClientId, GrantType) SELECT @NewClientId, GrantType FROM ClientGrantTypes WHERE ClientId = @SourceClientId",
                newClientId, tenant1ClientId);

            // Copy scopes
            await ExecuteCopyQuery(connection,
                "INSERT INTO ClientScopes (ClientId, Scope) SELECT @NewClientId, Scope FROM ClientScopes WHERE ClientId = @SourceClientId",
                newClientId, tenant1ClientId);

            // Add redirect URIs
            await ExecuteInsertUri(connection, "ClientRedirectUris", "RedirectUri", newClientId, $"https://{tenantId}.localhost:5001/signin-oidc");

            // Add post-logout redirect URIs
            await ExecuteInsertUri(connection, "ClientPostLogoutRedirectUris", "PostLogoutRedirectUri", newClientId, $"https://{tenantId}.localhost:3000");
            await ExecuteInsertUri(connection, "ClientPostLogoutRedirectUris", "PostLogoutRedirectUri", newClientId, $"https://{tenantId}.localhost:5001/signout-callback-oidc");

            // Add CORS origins
            await ExecuteInsertUri(connection, "ClientCorsOrigins", "Origin", newClientId, $"https://{tenantId}.localhost:3000");
            await ExecuteInsertUri(connection, "ClientCorsOrigins", "Origin", newClientId, $"https://{tenantId}.localhost:5001");
        }

        private async Task<int> GetClientId(SqlConnection connection, string clientId)
        {
            using (var command = new SqlCommand("SELECT Id FROM Clients WHERE ClientId = @ClientId", connection))
            {
                command.Parameters.AddWithValue("@ClientId", clientId);
                return (int)await command.ExecuteScalarAsync();
            }
        }

        private async Task ExecuteCopyQuery(SqlConnection connection, string query, int newClientId, int sourceClientId)
        {
            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@NewClientId", newClientId);
                command.Parameters.AddWithValue("@SourceClientId", sourceClientId);
                await command.ExecuteNonQueryAsync();
            }
        }

        private async Task ExecuteInsertUri(SqlConnection connection, string tableName, string columnName, int clientId, string value)
        {
            var query = $"INSERT INTO {tableName} (ClientId, {columnName}) VALUES (@ClientId, @Value)";
            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@ClientId", clientId);
                command.Parameters.AddWithValue("@Value", value);
                await command.ExecuteNonQueryAsync();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // DATABASE MANAGEMENT ENDPOINTS
        // ═══════════════════════════════════════════════════════════════

        // GET: api/admin/tenants/{tenantId}/database-config
        [HttpGet("tenants/{tenantId}/database-config")]
        public async Task<IActionResult> GetTenantDatabaseConfig(string tenantId)
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    var query = @"
                SELECT TenantId, Name, UseOwnDatabase, DatabaseMigrationStatus, 
                       LastMigrationDate,
                       CASE WHEN CustomConnectionString IS NOT NULL THEN 1 ELSE 0 END as HasCustomConnection
                FROM Tenants 
                WHERE TenantId = @TenantId";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@TenantId", tenantId);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                var config = new
                                {
                                    tenantId = reader["TenantId"].ToString(),
                                    name = reader["Name"].ToString(),
                                    useOwnDatabase = Convert.ToBoolean(reader["UseOwnDatabase"]),
                                    databaseMigrationStatus = reader["DatabaseMigrationStatus"]?.ToString(),
                                    lastMigrationDate = reader["LastMigrationDate"] != DBNull.Value
                                        ? Convert.ToDateTime(reader["LastMigrationDate"])
                                        : (DateTime?)null,
                                    hasCustomConnection = Convert.ToBoolean(reader["HasCustomConnection"]),
                                    globalMultiDbMode = _configuration.GetValue<bool>("IdentityDbSetup:MultiDatabase", false)
                                };

                                return Ok(new { success = true, config });
                            }
                        }
                    }
                }

                return NotFound(new { success = false, error = "Tenant not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching tenant database configuration");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // POST: api/admin/tenants/{tenantId}/database-config/test
        [HttpPost("tenants/{tenantId}/database-config/test")]
        public async Task<IActionResult> TestDatabaseConnection(string tenantId, [FromBody] TestConnectionRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.ConnectionString))
                {
                    return BadRequest(new { success = false, error = "Connection string is required" });
                }

                // Test the connection
                using var connection = new SqlConnection(request.ConnectionString);
                await connection.OpenAsync();

                // Test a simple query
                using var command = new SqlCommand("SELECT 1", connection);
                await command.ExecuteScalarAsync();

                // Get database name
                var databaseName = connection.Database;

                return Ok(new
                {
                    success = true,
                    message = "Connection successful",
                    databaseName,
                    serverVersion = connection.ServerVersion
                });
            }
            catch (SqlException sqlEx)
            {
                _logger.LogWarning(sqlEx, "Connection test failed for tenant {TenantId}", tenantId);
                return Ok(new
                {
                    success = false,
                    error = $"Connection failed: {sqlEx.Message}",
                    errorCode = sqlEx.Number
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing connection for tenant {TenantId}", tenantId);
                return Ok(new { success = false, error = $"Connection test failed: {ex.Message}" });
            }
        }

        // PUT: api/admin/tenants/{tenantId}/database-config
        [HttpPut("tenants/{tenantId}/database-config")]
        public async Task<IActionResult> UpdateTenantDatabaseConfig(string tenantId, [FromBody] UpdateDatabaseConfigRequest request)
        {
            try
            {
                if (tenantId.Equals("admin", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { success = false, error = "Cannot modify admin tenant database configuration" });
                }

                // Validate request
                if (request.UseOwnDatabase && string.IsNullOrWhiteSpace(request.CustomConnectionString))
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = "Custom connection string is required when UseOwnDatabase is true"
                    });
                }

                var connectionString = _configuration.GetConnectionString("DefaultConnection");

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    var query = @"
                UPDATE Tenants 
                SET UseOwnDatabase = @UseOwnDatabase,
                    CustomConnectionString = @CustomConnectionString,
                    DatabaseMigrationStatus = @MigrationStatus,
                    UpdatedAt = GETUTCDATE()
                WHERE TenantId = @TenantId";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@TenantId", tenantId);
                        command.Parameters.AddWithValue("@UseOwnDatabase", request.UseOwnDatabase);
                        command.Parameters.AddWithValue("@CustomConnectionString",
                            (object?)request.CustomConnectionString ?? DBNull.Value);

                        // Set migration status
                        string migrationStatus = request.UseOwnDatabase && !string.IsNullOrEmpty(request.CustomConnectionString)
                            ? "Pending"
                            : "NotConfigured";
                        command.Parameters.AddWithValue("@MigrationStatus", migrationStatus);

                        var rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected == 0)
                        {
                            return NotFound(new { success = false, error = "Tenant not found" });
                        }
                    }
                }

                return Ok(new
                {
                    success = true,
                    message = "Database configuration updated successfully",
                    requiresMigration = request.UseOwnDatabase && !string.IsNullOrEmpty(request.CustomConnectionString)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating tenant database configuration");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // POST: api/admin/tenants/{tenantId}/database-config/migrate
        [HttpPost("tenants/{tenantId}/database-config/migrate")]
        public async Task<IActionResult> RunMigrations(string tenantId)
        {
            try
            {
                // Get IdentityServer URL from configuration
                var identityServerUrl = _configuration.GetValue<string>("IdentityServerUrl") ?? "https://localhost:7140";

                // Forward the migration request to IdentityServer's Admin API
                var client = _httpClientFactory.CreateClient();
                var response = await client.PostAsync(
                    $"{identityServerUrl}/api/admin/migrate/{tenantId}",
                    null);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return Ok(new
                    {
                        success = true,
                        message = "Migration request forwarded to IdentityServer",
                        details = content
                    });
                }
                else
                {
                    return StatusCode((int)response.StatusCode, new
                    {
                        success = false,
                        error = "Failed to forward migration request to IdentityServer"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding migration request for tenant {TenantId}", tenantId);
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // GET: api/admin/system-info
        [HttpGet("system-info")]
        public IActionResult GetSystemInfo()
        {
            try
            {
                var multiDbEnabled = _configuration.GetValue<bool>("IdentityDbSetup:MultiDatabase", false);
                var defaultConnection = _configuration.GetConnectionString("DefaultConnection");

                var builder = new SqlConnectionStringBuilder(defaultConnection);

                return Ok(new
                {
                    success = true,
                    system = new
                    {
                        globalMultiDatabaseMode = multiDbEnabled,
                        databaseServer = builder.DataSource,
                        defaultDatabase = builder.InitialCatalog,
                        databaseNamingPattern = multiDbEnabled
                            ? "DuendeIdentityServer_{tenantId}"
                            : builder.InitialCatalog,
                        description = multiDbEnabled
                            ? "Global multi-database mode: Each tenant gets auto-generated database unless overridden"
                            : "Shared database mode: All tenants share single database unless individually configured"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching system info");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // Helper method to update migration status
        private async Task UpdateMigrationStatus(string tenantId, string status, DateTime? migrationDate)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            var query = @"
        UPDATE Tenants 
        SET DatabaseMigrationStatus = @Status,
            LastMigrationDate = @MigrationDate
        WHERE TenantId = @TenantId";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@TenantId", tenantId);
            command.Parameters.AddWithValue("@Status", status);
            command.Parameters.AddWithValue("@MigrationDate", (object?)migrationDate ?? DBNull.Value);

            await command.ExecuteNonQueryAsync();
        }
    }

    public class LockUserRequest
    {
        public bool Lock { get; set; }
    }

    public class CreateTenantRequest
    {
        public string TenantId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string? PrimaryColor { get; set; }
    }

    public class TestConnectionRequest
    {
        public string ConnectionString { get; set; } = string.Empty;
    }

    public class UpdateDatabaseConfigRequest
    {
        public bool UseOwnDatabase { get; set; }
        public string? CustomConnectionString { get; set; }
    }
}