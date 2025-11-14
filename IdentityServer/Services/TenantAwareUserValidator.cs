using IdentityServer.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Services;

public class TenantAwareUserValidator : IUserValidator<ApplicationUser>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantAwareUserValidator(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user)
    {
        var errors = new List<IdentityError>();

        // Get tenant from HttpContext
        var tenantId = _httpContextAccessor.HttpContext?.Items["TenantId"] as string;

        if (string.IsNullOrEmpty(tenantId))
        {
            errors.Add(new IdentityError
            {
                Code = "TenantRequired",
                Description = "Tenant context is required."
            });
            return IdentityResult.Failed(errors.ToArray());
        }

        // ONLY validate uniqueness WITHIN the same tenant
        // Username can be duplicate across tenants - only unique within tenant
        if (!string.IsNullOrEmpty(user.UserName))
        {
            var existingUser = await manager.Users
                .FirstOrDefaultAsync(u => u.UserName == user.UserName
                                       && u.TenantId == tenantId
                                       && u.Id != user.Id);

            if (existingUser != null)
            {
                errors.Add(new IdentityError
                {
                    Code = "DuplicateUserName",
                    Description = $"Username '{user.UserName}' is already taken in this organization."
                });
            }
        }

        // Email can be duplicate across tenants - only unique within tenant
        if (!string.IsNullOrEmpty(user.Email))
        {
            var existingEmail = await manager.Users
                .FirstOrDefaultAsync(u => u.Email == user.Email
                                       && u.TenantId == tenantId
                                       && u.Id != user.Id);

            if (existingEmail != null)
            {
                errors.Add(new IdentityError
                {
                    Code = "DuplicateEmail",
                    Description = $"Email '{user.Email}' is already registered in this organization."
                });
            }
        }

        // Phone number, DOB, country, etc. are NOT validated for uniqueness
        // They can be completely identical across tenants

        return errors.Count > 0 ? IdentityResult.Failed(errors.ToArray()) : IdentityResult.Success;
    }
}