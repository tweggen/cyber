using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NotebookAdmin.Components;
using NotebookAdmin.Data;
using NotebookAdmin.Models;
using NotebookAdmin.Services;

var builder = WebApplication.CreateBuilder(args);

// Add EF Core with PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
    options.User.RequireUniqueEmail = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure cookie auth for Blazor Server
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/auth/login";
    options.LogoutPath = "/auth/logout";
    options.AccessDeniedPath = "/auth/access-denied";
});

// Add JWT token service (EdDSA signing for Rust API auth)
builder.Services.AddSingleton<TokenService>();

// Add Notebook API client
builder.Services.AddHttpClient<NotebookApiClient>(client =>
{
    var baseUrl = builder.Configuration["NotebookApi:BaseUrl"] ?? "http://localhost:3000";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

builder.Services.AddScoped<AuthorService>();
builder.Services.AddScoped<QuotaService>();
builder.Services.AddScoped<CurrentUserService>();

// Add Razor Components with Server interactivity
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

// Ensure the target database exists (safe for Coolify where PG init scripts may not run)
{
    var connString = builder.Configuration.GetConnectionString("DefaultConnection");
    var connBuilder = new NpgsqlConnectionStringBuilder(connString);
    var dbName = connBuilder.Database!;
    connBuilder.Database = "postgres";
    using var conn = new NpgsqlConnection(connBuilder.ConnectionString);
    conn.Open();
    using var check = conn.CreateCommand();
    check.CommandText = $"SELECT 1 FROM pg_database WHERE datname = '{dbName}'";
    if (check.ExecuteScalar() == null)
    {
        using var create = conn.CreateCommand();
        create.CommandText = $"CREATE DATABASE \"{dbName}\"";
        create.ExecuteNonQuery();
        app.Logger.LogInformation("Created database {Database}", dbName);
    }
}

// Run EF Core migrations automatically on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

// Seed initial admin user from environment variables (if set)
{
    var adminUser = app.Configuration["ADMIN_USERNAME"];
    var adminPass = app.Configuration["ADMIN_PASSWORD"];
    if (!string.IsNullOrEmpty(adminUser) && !string.IsNullOrEmpty(adminPass))
    {
        using var scope = app.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var existing = await userManager.FindByNameAsync(adminUser);
        if (existing == null)
        {
            try
            {
                var authorService = scope.ServiceProvider.GetRequiredService<AuthorService>();
                var quotaService = scope.ServiceProvider.GetRequiredService<QuotaService>();
                var (authorIdHex, authorIdBytes) = await authorService.RegisterNewAuthorAsync();

                var user = new ApplicationUser
                {
                    UserName = adminUser,
                    DisplayName = "Admin",
                    AuthorId = authorIdBytes,
                    AuthorIdHex = authorIdHex,
                };

                var result = await userManager.CreateAsync(user, adminPass);
                if (result.Succeeded)
                {
                    await quotaService.GetOrCreateDefaultAsync(user.Id);
                    app.Logger.LogInformation("Seeded admin user '{Username}'", adminUser);
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    app.Logger.LogWarning("Failed to seed admin user: {Errors}", errors);
                }
            }
            catch (Exception ex)
            {
                app.Logger.LogWarning(ex,
                    "Could not seed admin user (notebook API may not be ready yet). " +
                    "Use POST /auth/register once all services are running.");
            }
        }
    }
}

// Configure the HTTP request pipeline
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Registration endpoint for API clients (AI agents, tools, etc.)
app.MapPost("/auth/register", async (
    RegisterRequest request,
    UserManager<ApplicationUser> userManager,
    AuthorService authorService,
    QuotaService quotaService) =>
{
    // Validate input
    if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        return Results.BadRequest(new { error = "Username and password are required" });

    // Register author with the Rust notebook API
    var (authorIdHex, authorIdBytes) = await authorService.RegisterNewAuthorAsync();

    var user = new ApplicationUser
    {
        UserName = request.Username,
        DisplayName = request.DisplayName,
        AuthorId = authorIdBytes,
        AuthorIdHex = authorIdHex,
    };

    var result = await userManager.CreateAsync(user, request.Password);
    if (!result.Succeeded)
    {
        var errors = result.Errors.Select(e => e.Description).ToList();
        return Results.BadRequest(new { errors });
    }

    // Auto-assign default quota
    await quotaService.GetOrCreateDefaultAsync(user.Id);

    return Results.Ok(new { authorId = authorIdHex, username = request.Username });
}).AllowAnonymous();

// Token endpoint for API clients (AI agents, tools, etc.)
app.MapPost("/auth/token", async (
    TokenRequest request,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    TokenService tokenService) =>
{
    var user = await userManager.FindByNameAsync(request.Username);
    if (user == null)
        return Results.Unauthorized();

    var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);
    if (!result.Succeeded)
        return Results.Unauthorized();

    var token = tokenService.GenerateToken(user.AuthorIdHex);
    var expiryMinutes = int.TryParse(
        app.Configuration["Jwt:ExpiryMinutes"], out var exp) ? exp : 60;

    return Results.Ok(new TokenResponse
    {
        Token = token,
        AuthorId = user.AuthorIdHex,
        ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes).ToUnixTimeSeconds(),
    });
}).AllowAnonymous();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
