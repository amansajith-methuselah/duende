using IdentityServer.Data;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Middleware;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext)
    {
        var host = context.Request.Host.Value; // e.g., "tenant1.localhost:7140"

        _logger.LogInformation("=== TENANT RESOLUTION === Host: {Host}", host);

        // Look up tenant by domain
        var tenant = await dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Domain == host && t.IsActive);

        if (tenant != null)
        {
            _logger.LogInformation("=== TENANT FOUND === TenantId: {TenantId}, Name: {Name}", tenant.TenantId, tenant.Name);

            // Store tenant info in HttpContext for later use
            context.Items["TenantId"] = tenant.TenantId;
            context.Items["Tenant"] = tenant;
        }
        else
        {
            _logger.LogWarning("=== TENANT NOT FOUND === Host: {Host}", host);
        }

        await _next(context);
    }
}