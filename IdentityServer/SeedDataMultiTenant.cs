using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Models;
using IdentityServer.Data;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer;

public class SeedDataMultiTenant
{
    public static async Task EnsureSeedData(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var configContext = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
        var appContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Get all tenants
        var tenants = await appContext.Tenants.Where(t => t.IsActive).ToListAsync();

        // Seed Identity Resources (shared across all tenants)
        await SeedIdentityResources(configContext);

        // Seed API Scopes (shared across all tenants)
        await SeedApiScopes(configContext);

        // Seed tenant-specific clients
        foreach (var tenant in tenants)
        {
            await SeedTenantClients(configContext, tenant);
        }
    }

    private static async Task SeedIdentityResources(ConfigurationDbContext context)
    {
        if (!await context.IdentityResources.AnyAsync())
        {
            var resources = new List<IdentityResource>
            {
                new IdentityResources.OpenId(),
                new IdentityResources.Profile(),
                new IdentityResources.Email(),
                new IdentityResource("phone", new[] { "phone_number" }),
                new IdentityResource("country", new[] { "country" }),
                new IdentityResource("birthdate", new[] { "birthdate" })
            };

            foreach (var resource in resources)
            {
                context.IdentityResources.Add(resource.ToEntity());
            }

            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedApiScopes(ConfigurationDbContext context)
    {
        if (!await context.ApiScopes.AnyAsync())
        {
            var scopes = new List<ApiScope>
            {
                new ApiScope("api1", "My API")
            };

            foreach (var scope in scopes)
            {
                context.ApiScopes.Add(scope.ToEntity());
            }

            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedTenantClients(ConfigurationDbContext context, Tenant tenant)
    {
        var clientId = $"bff-client-{tenant.TenantId}";

        // Check if client already exists
        var existingClient = await context.Clients
            .FirstOrDefaultAsync(c => c.ClientId == clientId);

        if (existingClient != null)
        {
            return; // Client already exists for this tenant
        }

        var client = new Client
        {
            ClientId = clientId,
            ClientName = $"BFF Client for {tenant.Name}",

            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            RequireClientSecret = false,

            RedirectUris = { $"https://{tenant.Domain.Replace(":7140", ":5001")}/signin-oidc" },
            PostLogoutRedirectUris = { $"https://{tenant.Domain.Replace(":7140", ":5001")}/signout-callback-oidc" },
            FrontChannelLogoutUri = $"https://{tenant.Domain.Replace(":7140", ":5001")}/signout-oidc",

            AllowedScopes = { "openid", "profile", "email", "phone", "country", "birthdate", "api1" },
            AllowOfflineAccess = true,

            RefreshTokenUsage = TokenUsage.ReUse,
            RefreshTokenExpiration = TokenExpiration.Sliding,
            SlidingRefreshTokenLifetime = 3600,

            AlwaysIncludeUserClaimsInIdToken = true
        };

        var clientEntity = client.ToEntity();
        context.Clients.Add(clientEntity);
        await context.SaveChangesAsync();

        // Now set TenantId via raw SQL since the entity doesn't expose it
        var insertedClientId = clientEntity.Id;
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE Clients SET TenantId = {0} WHERE Id = {1}",
            tenant.TenantId, insertedClientId);
    }
}