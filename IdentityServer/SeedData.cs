using IdentityServer.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace IdentityServer;

public class SeedData
{
    public static async Task EnsureSeedData(WebApplication app)
    {
        using (var scope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope())
        {
            var context = scope.ServiceProvider.GetService<ApplicationDbContext>();
            await context.Database.MigrateAsync();

            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var alice = await userMgr.FindByNameAsync("alice");
            if (alice == null)
            {
                alice = new ApplicationUser
                {
                    UserName = "alice",
                    Email = "alice@email.com",
                    EmailConfirmed = true,
                };
                var result = await userMgr.CreateAsync(alice, "Pass123$");
                if (!result.Succeeded)
                {
                    throw new Exception(result.Errors.First().Description);
                }

                result = await userMgr.AddClaimsAsync(alice, new Claim[]{
                    new Claim("name", "Alice Smith"),
                    new Claim("given_name", "Alice"),
                    new Claim("family_name", "Smith"),
                });
                if (!result.Succeeded)
                {
                    throw new Exception(result.Errors.First().Description);
                }
            }
        }
    }
}