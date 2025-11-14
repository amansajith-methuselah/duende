using IdentityServer;
using IdentityServer.Data;
using IdentityServer.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add MVC services (instead of just RazorPages)
builder.Services.AddControllersWithViews();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Add DbContext (default/shared)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// ???????????????????????????????????????????????????????????
// MULTI-DATABASE SUPPORT - Register Services
// ???????????????????????????????????????????????????????????
builder.Services.AddSingleton<ITenantConnectionResolver, TenantConnectionResolver>();
builder.Services.AddScoped<ITenantDbContextFactory, TenantDbContextFactory>();

// Configure cookie settings
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "IdentityServer.Cookie";
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(2);
    options.SlidingExpiration = true;
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// Add ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // CRITICAL: Disable all default unique checks
    options.User.RequireUniqueEmail = false;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// CRITICAL: Remove ALL default validators and add ONLY our tenant-aware validator
builder.Services.AddScoped<IUserValidator<ApplicationUser>>(services =>
    new TenantAwareUserValidator(services.GetRequiredService<IHttpContextAccessor>()));

// Remove default UserValidator (enforces global unique username)
builder.Services.Configure<IdentityOptions>(options =>
{
    options.User.RequireUniqueEmail = false;
});

// Add IdentityServer with EntityFramework stores
var migrationsAssembly = typeof(Program).Assembly.GetName().Name;

builder.Services.AddIdentityServer(options =>
{
    options.Events.RaiseErrorEvents = true;
    options.Events.RaiseInformationEvents = true;
    options.Events.RaiseFailureEvents = true;
    options.Events.RaiseSuccessEvents = true;

    options.Authentication.CookieLifetime = TimeSpan.FromHours(2);
    options.Authentication.CookieSlidingExpiration = true;

    // LEAVE IssuerUri BLANK - will be set dynamically per request
    options.IssuerUri = null;
}).AddConfigurationStore(options =>
    {
        options.ConfigureDbContext = b => b.UseSqlServer(connectionString,
            sql => sql.MigrationsAssembly(migrationsAssembly));
    })
    .AddOperationalStore(options =>
    {
        options.ConfigureDbContext = b => b.UseSqlServer(connectionString,
            sql => sql.MigrationsAssembly(migrationsAssembly));
    })
    .AddAspNetIdentity<ApplicationUser>()
    .AddProfileService<ProfileService>();

// Register custom tenant-aware client store
builder.Services.AddTransient<Duende.IdentityServer.Stores.IClientStore, IdentityServer.Services.TenantAwareClientStore>();

// Required for tenant resolution in client store
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Seed data
//await SeedData.EnsureSeedData(app);
await SeedDataMultiTenant.EnsureSeedData(app.Services);

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Add tenant resolution middleware
app.UseMiddleware<IdentityServer.Middleware.TenantResolutionMiddleware>();

// CRITICAL: Set dynamic issuer based on request host (tenant-aware)
app.Use(async (context, next) =>
{
    var options = context.RequestServices.GetRequiredService<Duende.IdentityServer.Configuration.IdentityServerOptions>();

    // Get the current request host (e.g., "tenant1.localhost:7140" or "tenant2.localhost:7140")
    var host = context.Request.Host.Value;

    // Set the issuer dynamically to match the tenant subdomain
    options.IssuerUri = $"https://{host}";

    await next();
});


app.UseIdentityServer();

app.UseAuthorization();

// Map MVC controllers
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();