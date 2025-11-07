using Duende.IdentityServer.Models;

namespace IdentityServer;

public static class Config
{
    public static IEnumerable<IdentityResource> IdentityResources =>
        new IdentityResource[]
        {
            new IdentityResources.OpenId(),
            new IdentityResources.Profile(),
            new IdentityResources.Email(),
            new IdentityResources.Phone(),
            // Custom identity resource for additional claims
            new IdentityResource(
                name: "custom.profile",
                userClaims: new[] { "country", "birthdate" },
                displayName: "Custom Profile Information")
        };

    public static IEnumerable<ApiScope> ApiScopes =>
        new ApiScope[]
        {
            new ApiScope("api1", "My API")
        };

    public static IEnumerable<Client> Clients =>
        new Client[]
        {
            // Interactive client (old ClientApp)
            new Client
            {
                ClientId = "interactive",
                ClientSecrets = { new Secret("secret".Sha256()) },

                AllowedGrantTypes = GrantTypes.Code,

                RedirectUris = { "https://localhost:5003/signin-oidc" },
                PostLogoutRedirectUris = { "https://localhost:5003/signout-callback-oidc" },

                AllowedScopes = { "openid", "profile", "api1" },

                RequirePkce = true,
                AllowPlainTextPkce = false
            },
            // BFF client
new Client
{
    ClientId = "bff-client",
    ClientSecrets = { new Secret("secret".Sha256()) },

    AllowedGrantTypes = GrantTypes.Code,

    RedirectUris = { "https://localhost:5001/signin-oidc" },
    FrontChannelLogoutUri = "https://localhost:5001/signout",
    PostLogoutRedirectUris = {
        "https://localhost:5001/signout-callback-oidc",
        "https://localhost:5001",
        "https://localhost:3000"  // Add React app URL
    },

    AllowedScopes = {
        "openid",
        "profile",
        "email",
        "phone",
        "custom.profile",
        "api1"
    },
    AllowOfflineAccess = true,

    RequirePkce = true,
    AllowPlainTextPkce = false
}
        };
}