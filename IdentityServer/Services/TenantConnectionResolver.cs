using IdentityServer.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Services
{
    public interface ITenantConnectionResolver
    {
        Task<string> GetConnectionStringAsync(string tenantId);
        Task<bool> ValidateConnectionStringAsync(string connectionString);
        Task<bool> RunMigrationsAsync(string connectionString);
        void ClearCache(string tenantId);
    }

    public class TenantConnectionResolver : ITenantConnectionResolver
    {
        private readonly string _defaultConnectionString;
        private readonly bool _globalMultiDatabaseEnabled;
        private readonly ILogger<TenantConnectionResolver> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<string, string> _connectionCache = new();

        public TenantConnectionResolver(
            IConfiguration configuration,
            ILogger<TenantConnectionResolver> logger,
            IServiceProvider serviceProvider)
        {
            _defaultConnectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentException("Default connection string not found");

            _globalMultiDatabaseEnabled = configuration.GetValue<bool>("IdentityDbSetup:MultiDatabase", false);
            _logger = logger;
            _serviceProvider = serviceProvider;

            _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            _logger.LogInformation("Global Multi-Database Mode: {Enabled}",
                _globalMultiDatabaseEnabled ? "ENABLED ✓" : "DISABLED");
            _logger.LogInformation("Per-Tenant Override: ENABLED ✓");
            _logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        }

        public async Task<string> GetConnectionStringAsync(string tenantId)
        {
            // Check cache first
            if (_connectionCache.ContainsKey(tenantId))
            {
                return _connectionCache[tenantId];
            }

            try
            {
                // Query tenant configuration from default database
                using var scope = _serviceProvider.CreateScope();
                var defaultContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var tenant = await defaultContext.Tenants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.TenantId == tenantId);

                if (tenant == null)
                {
                    _logger.LogWarning("Tenant {TenantId} not found, using default connection", tenantId);
                    return _defaultConnectionString;
                }

                string connectionString;

                // PRIORITY 1: Tenant-specific custom connection string (highest priority)
                if (tenant.UseOwnDatabase && !string.IsNullOrEmpty(tenant.CustomConnectionString))
                {
                    connectionString = tenant.CustomConnectionString;
                    _logger.LogInformation("Tenant {TenantId} → Custom Connection String (Priority 1)", tenantId);
                }
                // PRIORITY 2: Global multi-database mode
                else if (_globalMultiDatabaseEnabled && !tenant.UseOwnDatabase)
                {
                    var builder = new SqlConnectionStringBuilder(_defaultConnectionString);
                    builder.InitialCatalog = $"DuendeIdentityServer_{tenantId}";
                    connectionString = builder.ConnectionString;
                    _logger.LogInformation("Tenant {TenantId} → Auto Database: {DbName} (Priority 2)",
                        tenantId, builder.InitialCatalog);
                }
                // PRIORITY 3: Shared database (default/lowest priority)
                else
                {
                    connectionString = _defaultConnectionString;
                    _logger.LogDebug("Tenant {TenantId} → Shared Database (Priority 3)", tenantId);
                }

                // Cache the connection string
                _connectionCache[tenantId] = connectionString;

                return connectionString;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving connection for tenant {TenantId}, using default", tenantId);
                return _defaultConnectionString;
            }
        }

        public async Task<bool> ValidateConnectionStringAsync(string connectionString)
        {
            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                // Test a simple query
                using var command = new SqlCommand("SELECT 1", connection);
                await command.ExecuteScalarAsync();

                _logger.LogInformation("✓ Connection string validated successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "✗ Connection string validation failed");
                return false;
            }
        }

        public async Task<bool> RunMigrationsAsync(string connectionString)
        {
            try
            {
                _logger.LogInformation("Starting migration on custom database...");

                var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
                optionsBuilder.UseSqlServer(connectionString);

                using var context = new ApplicationDbContext(optionsBuilder.Options);

                // Run migrations
                await context.Database.MigrateAsync();

                _logger.LogInformation("✓ Migrations completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "✗ Migration failed");
                return false;
            }
        }

        public void ClearCache(string tenantId)
        {
            if (_connectionCache.ContainsKey(tenantId))
            {
                _connectionCache.Remove(tenantId);
                _logger.LogInformation("Cache cleared for tenant {TenantId}", tenantId);
            }
        }
    }
}