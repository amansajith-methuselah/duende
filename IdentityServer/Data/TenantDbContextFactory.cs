using IdentityServer.Services;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Data
{
    public interface ITenantDbContextFactory
    {
        Task<ApplicationDbContext> CreateDbContextAsync(string tenantId);
    }

    public class TenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly ITenantConnectionResolver _connectionResolver;
        private readonly ILogger<TenantDbContextFactory> _logger;

        public TenantDbContextFactory(
            ITenantConnectionResolver connectionResolver,
            ILogger<TenantDbContextFactory> logger)
        {
            _connectionResolver = connectionResolver;
            _logger = logger;
        }

        public async Task<ApplicationDbContext> CreateDbContextAsync(string tenantId)
        {
            _logger.LogInformation("Creating DbContext for tenant: {TenantId}", tenantId);

            // Get the correct connection string for this tenant
            var connectionString = await _connectionResolver.GetConnectionStringAsync(tenantId);

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseSqlServer(connectionString);

            var context = new ApplicationDbContext(optionsBuilder.Options);

            try
            {
                // Ensure database exists and run migrations
                _logger.LogInformation("Ensuring database exists and running migrations for tenant {TenantId}...", tenantId);
                await context.Database.MigrateAsync();
                _logger.LogInformation("✓ Database ready for tenant {TenantId}", tenantId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "✗ Failed to prepare database for tenant {TenantId}", tenantId);
                // Don't throw - return context anyway and let caller handle
            }

            return context;
        }
    }
}