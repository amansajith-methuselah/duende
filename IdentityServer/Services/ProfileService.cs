using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using IdentityServer.Data;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace IdentityServer.Services
{
    public class ProfileService : IProfileService
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ProfileService(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task GetProfileDataAsync(ProfileDataRequestContext context)
        {
            var user = await _userManager.GetUserAsync(context.Subject);
            if (user == null)
            {
                return;
            }

            // Get user claims from database
            var userClaims = await _userManager.GetClaimsAsync(user);

            // Get user roles
            var roles = await _userManager.GetRolesAsync(user);

            var claims = new List<Claim>
    {
        new Claim("sub", user.Id),
        new Claim("email", user.Email ?? ""),
        new Claim("email_verified", user.EmailConfirmed.ToString().ToLower()),
        new Claim("preferred_username", user.UserName ?? ""),
    };

            // Add phone if exists
            if (!string.IsNullOrEmpty(user.PhoneNumber))
            {
                claims.Add(new Claim("phone_number", user.PhoneNumber));
            }

            // CRITICAL: Add tenant_id claim
            if (!string.IsNullOrEmpty(user.TenantId))
            {
                claims.Add(new Claim("tenant_id", user.TenantId));
            }

            // Add roles as claims
            foreach (var role in roles)
            {
                claims.Add(new Claim("role", role));
            }

            // Add custom claims from AspNetUserClaims table
            foreach (var claim in userClaims)
            {
                // Add name, given_name, family_name, birthdate, country claims if they exist
                if (claim.Type == "name" ||
                    claim.Type == "given_name" ||
                    claim.Type == "family_name" ||
                    claim.Type == "birthdate" ||
                    claim.Type == "country")
                {
                    claims.Add(claim);
                }
            }

            // Add requested claims only
            context.IssuedClaims.AddRange(claims.Where(c => context.RequestedClaimTypes.Contains(c.Type)));
        }

        public async Task IsActiveAsync(IsActiveContext context)
        {
            var user = await _userManager.GetUserAsync(context.Subject);

            if (user == null)
            {
                context.IsActive = false;
                return;
            }

            // Check if user is locked out
            var isLockedOut = await _userManager.IsLockedOutAsync(user);
            context.IsActive = !isLockedOut;
        }
    }
}