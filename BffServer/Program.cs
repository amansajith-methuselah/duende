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
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

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
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
})
.AddOpenIdConnect("oidc", options =>
{
    options.Authority = "https://localhost:7140";
    options.ClientId = "bff-client";
    options.ClientSecret = "secret";
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
    options.Scope.Add("custom.profile");
    options.Scope.Add("api1");
    options.Scope.Add("offline_access");

    options.TokenValidationParameters.NameClaimType = "name";
    options.TokenValidationParameters.RoleClaimType = "role";

    options.RequireHttpsMetadata = true;

    // Set callback paths
    options.CallbackPath = "/signin-oidc";
    options.SignedOutCallbackPath = "/signout-callback-oidc";

    // IMPORTANT: Configure events to redirect to React app after authentication
    options.Events.OnRedirectToIdentityProvider = context =>
    {
        // Store the original return URL
        if (string.IsNullOrEmpty(context.Properties.RedirectUri))
        {
            context.Properties.RedirectUri = "/";
        }
        return Task.CompletedTask;
    };

    options.Events.OnTicketReceived = context =>
    {
        // After successful authentication, redirect to React app
        context.Properties.RedirectUri = "https://localhost:3000";
        return Task.CompletedTask;
    };
});

// Add BFF services
builder.Services.AddBff();

// Add authorization
builder.Services.AddAuthorization();

// Add HttpClient factory
builder.Services.AddHttpClient();

// Add CORS for React app
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins("https://localhost:3000", "http://localhost:3000")
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

app.Run();