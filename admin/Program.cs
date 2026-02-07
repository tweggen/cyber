using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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

// Add Razor Components with Server interactivity
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Registration endpoint for API clients (AI agents, tools, etc.)
app.MapPost("/auth/register", async (
    RegisterRequest request,
    UserManager<ApplicationUser> userManager,
    AuthorService authorService) =>
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
