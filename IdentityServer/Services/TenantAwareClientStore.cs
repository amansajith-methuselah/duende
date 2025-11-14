using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Services;

public class TenantAwareClientStore : IClientStore
{
    private readonly ConfigurationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<TenantAwareClientStore> _logger;

    public TenantAwareClientStore(
        ConfigurationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        ILogger<TenantAwareClientStore> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<Client?> FindClientByIdAsync(string clientId)
    {
        // Get current tenant from HttpContext
        var tenantId = _httpContextAccessor.HttpContext?.Items["TenantId"] as string;

        _logger.LogInformation("=== CLIENT LOOKUP === ClientId: {ClientId}, TenantId: {TenantId}",
            clientId, tenantId ?? "NULL");

        // First, get the client's TenantId using ExecuteSqlRaw with output parameter approach
        var clientTenantIdParam = new Microsoft.Data.SqlClient.SqlParameter
        {
            ParameterName = "@ClientTenantId",
            SqlDbType = System.Data.SqlDbType.NVarChar,
            Size = 50,
            Direction = System.Data.ParameterDirection.Output
        };

        await _context.Database.ExecuteSqlRawAsync(
            "SELECT @ClientTenantId = TenantId FROM Clients WHERE ClientId = {0}",
            new Microsoft.Data.SqlClient.SqlParameter("@ClientId", clientId),
            clientTenantIdParam);

        var clientTenantId = clientTenantIdParam.Value as string;

        if (clientTenantId == null && !await _context.Clients.AnyAsync(c => c.ClientId == clientId))
        {
            _logger.LogWarning("=== CLIENT NOT FOUND === ClientId: {ClientId}", clientId);
            return null;
        }

        // Check if client belongs to current tenant
        if (!string.IsNullOrEmpty(tenantId) && clientTenantId != tenantId)
        {
            _logger.LogWarning("=== TENANT MISMATCH === ClientId: {ClientId}, Expected: {ExpectedTenant}, Actual: {ActualTenant}",
                clientId, tenantId, clientTenantId ?? "NULL");
            return null; // Client doesn't belong to this tenant
        }

        // Load the full client with all navigation properties
        var client = await _context.Clients
            .Where(c => c.ClientId == clientId)
            .Include(c => c.AllowedGrantTypes)
            .Include(c => c.RedirectUris)
            .Include(c => c.PostLogoutRedirectUris)
            .Include(c => c.AllowedScopes)
            .Include(c => c.ClientSecrets)
            .Include(c => c.Claims)
            .Include(c => c.IdentityProviderRestrictions)
            .Include(c => c.AllowedCorsOrigins)
            .Include(c => c.Properties)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (client == null)
        {
            return null;
        }

        _logger.LogInformation("=== CLIENT FOUND === ClientId: {ClientId}, TenantId: {TenantId}",
            clientId, clientTenantId ?? "NULL");

        return client.ToModel();
    }
}