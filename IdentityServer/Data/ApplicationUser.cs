using Microsoft.AspNetCore.Identity;

namespace IdentityServer.Data;

public class ApplicationUser : IdentityUser
{
    public string? TenantId { get; set; }
}