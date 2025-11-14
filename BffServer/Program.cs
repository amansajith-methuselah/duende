using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

// Add MVC services
builder.Services.AddControllersWithViews();

// Add session support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.None; // Changed from Lax
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.Name = ".BFF.Session"; // Add explicit name
});

// Add HttpContextAccessor for accessing request context
builder.Services.AddHttpContextAccessor();

// Configure cookie authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Cookies";
    options.DefaultChallengeScheme = "oidc";
    options.DefaultSignOutScheme = "oidc";
})
.AddCookie("Cookies", options =>
{
    options.Cookie.Name = "__Host-bff";
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
})
.AddOpenIdConnect("oidc", options =>
{
    // These will be set dynamically per request
    options.Authority = "https://localhost:7140"; // Fallback only
    options.ClientId = "bff-client"; // Fallback only

    options.ResponseType = "code";
    options.ResponseMode = "query";
    options.GetClaimsFromUserInfoEndpoint = true;
    options.MapInboundClaims = false;
    options.SaveTokens = true;

    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");
    options.Scope.Add("phone");
    options.Scope.Add("country");
    options.Scope.Add("birthdate");
    options.Scope.Add("api1");
    options.Scope.Add("offline_access");
    options.Scope.Add("tenant_id");
    options.Scope.Add("role");

    options.TokenValidationParameters.NameClaimType = "name";
    options.TokenValidationParameters.RoleClaimType = "role";
    options.TokenValidationParameters.ValidateAudience = false;
    options.RequireHttpsMetadata = true;

    // Set callback paths
    options.CallbackPath = "/signin-oidc";
    options.SignedOutCallbackPath = "/signout-callback-oidc";

    // CRITICAL: Configure tenant-aware authentication
    options.Events.OnRedirectToIdentityProvider = context =>
    {
        var request = context.HttpContext.Request;
        var host = request.Host.Value; // e.g., "tenant1.localhost:5001"

        // Extract tenant subdomain
        var parts = host.Split('.');
        if (parts.Length >= 2 && parts[0] != "localhost")
        {
            var tenantId = parts[0]; // "tenant1", "tenant2", etc.

            // Set tenant-specific Authority and ClientId
            var identityServerHost = host.Replace(request.Host.Port?.ToString() ?? "5001", "7140");
            context.ProtocolMessage.IssuerAddress = $"https://{identityServerHost}/connect/authorize";

            // Set tenant-specific client ID
            context.ProtocolMessage.ClientId = $"bff-client-{tenantId}";
        }

        // Store the original return URL
        if (string.IsNullOrEmpty(context.Properties.RedirectUri))
        {
            context.Properties.RedirectUri = "/";
        }

        return Task.CompletedTask;
    };

    options.Events.OnAuthorizationCodeReceived = context =>
    {
        var request = context.HttpContext.Request;
        var host = request.Host.Value;

        var parts = host.Split('.');
        if (parts.Length >= 2 && parts[0] != "localhost")
        {
            var tenantId = parts[0];
            var identityServerHost = host.Replace(request.Host.Port?.ToString() ?? "5001", "7140");

            // Set tenant-specific Authority
            context.Options.Authority = $"https://{identityServerHost}";
            context.Options.ClientId = $"bff-client-{tenantId}";

            // CRITICAL: Override configuration to use tenant-specific endpoints
            context.Options.Configuration = new Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration
            {
                TokenEndpoint = $"https://{identityServerHost}/connect/token",
                AuthorizationEndpoint = $"https://{identityServerHost}/connect/authorize",
                UserInfoEndpoint = $"https://{identityServerHost}/connect/userinfo",
                EndSessionEndpoint = $"https://{identityServerHost}/connect/endsession",
                Issuer = $"https://{identityServerHost}"
            };

            if (context.TokenEndpointRequest != null)
            {
                context.TokenEndpointRequest.ClientId = $"bff-client-{tenantId}";
            }
        }

        return Task.CompletedTask;
    };

    options.Events.OnTicketReceived = context =>
    {
        // Don't override RedirectUri - let BFF handle it
        return Task.CompletedTask;
    };

    options.Events.OnRedirectToIdentityProviderForSignOut = context =>
    {
        // When initiating logout, set the tenant-specific end session endpoint
        var request = context.HttpContext.Request;
        var host = request.Host.Value;

        var parts = host.Split('.');
        if (parts.Length >= 2 && parts[0] != "localhost")
        {
            var tenantId = parts[0];
            var identityServerHost = host.Replace(request.Host.Port?.ToString() ?? "5001", "7140");

            // Set tenant-specific end session endpoint
            context.ProtocolMessage.IssuerAddress = $"https://{identityServerHost}/connect/endsession";

            // CRITICAL: Include id_token_hint so IdentityServer knows which user to logout
            var idToken = context.Properties.GetTokenValue("id_token");
            if (!string.IsNullOrEmpty(idToken))
            {
                context.ProtocolMessage.IdTokenHint = idToken;
            }

            // Set post logout redirect to React
            var reactHost = host.Replace(":5001", ":3000");
            context.ProtocolMessage.PostLogoutRedirectUri = $"https://{reactHost}";
        }

        return Task.CompletedTask;
    };
});

// Add BFF services - configure to allow tenant subdomains
builder.Services.AddBff(options =>
{
    options.ManagementBasePath = "/bff";
    options.LicenseKey = "";

    // Allow tenant subdomains as valid return URLs
    options.RequireLogoutSessionId = false;
    options.RevokeRefreshTokenOnLogout = false;
    options.BackchannelLogoutAllUserSessions = false;
});

// Configure BFF to accept tenant subdomains as valid origins
builder.Services.Configure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(options =>
{
    // This allows BFF to accept returnUrls from tenant subdomains
});

// Add authorization
builder.Services.AddAuthorization();

// Add HttpClient factory
builder.Services.AddHttpClient();

// Add CORS for React app - tenant-aware
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
        {
            // Allow any subdomain on localhost:3000
            var uri = new Uri(origin);
            return (uri.Host.EndsWith(".localhost") && uri.Port == 3000) ||
                   (uri.Host == "localhost" && uri.Port == 3000);
        })
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Enable CORS
app.UseCors("AllowReactApp");

// Add session before authentication
app.UseSession();

// Important: Order matters!
app.UseAuthentication();
app.UseBff();
app.UseAuthorization();

// Map MVC controllers
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// BFF management endpoints (login, logout, user info, etc.)
app.MapBffManagementEndpoints();

// Fallback for SPA
app.MapFallbackToFile("index.html");

// Add a simple home redirect
app.MapGet("/", (HttpContext context) =>
{
    if (context.User?.Identity?.IsAuthenticated == true)
    {
        // If authenticated, redirect to React
        var host = context.Request.Host.Value.Replace(":5001", ":3000");
        return Results.Redirect($"https://{host}");
    }
    return Results.Content(@"
        <!DOCTYPE html>
        <html>
        <head><title>BFF Server</title></head>
        <body>
            <h1>BFF Server Running</h1>
            <p>This is the backend-for-frontend server.</p>
            <p><a href='/bff/login'>Login</a></p>
        </body>
        </html>
    ", "text/html");
});

app.Run();