# Plan 03: Blazor Server Frontend

New .NET 8 Blazor Server project for web administration of the notebook platform.

## Prerequisites

- Plans 01 and 02 fully implemented (backend auth, user management, usage log API)
- .NET 8 SDK installed (`dotnet --version` shows 8.x)
- Notebook server running with auth enabled

## Step 1: Create Blazor Server Project

```bash
cd notebook/
mkdir frontend
cd frontend
dotnet new blazorserver -n NotebookFrontend -o . --framework net8.0
```

This creates the standard Blazor Server project structure. Then clean up the default template:
- Remove `Pages/Counter.razor`, `Pages/FetchData.razor`
- Remove `Data/WeatherForecastService.cs`, `Data/WeatherForecast.cs`
- Keep `Pages/Index.razor`, `Pages/Error.cshtml`, `Shared/MainLayout.razor`, `Shared/NavMenu.razor`

## Step 2: Add NuGet Packages

```bash
cd notebook/frontend
dotnet add package Microsoft.AspNetCore.Components.Authorization
dotnet add package System.IdentityModel.Tokens.Jwt
```

## Step 3: Project Structure

```
notebook/frontend/
  NotebookFrontend.csproj
  Program.cs
  appsettings.json
  appsettings.Development.json
  Dockerfile
  Services/
    NotebookApiClient.cs
    JwtAuthenticationStateProvider.cs
  Models/
    ApiModels.cs
  Pages/
    Index.razor              -> Dashboard (notebook list)
    Login.razor              -> Login page
    NotebookDetail.razor     -> Notebook metadata + participants + usage
    Users.razor              -> User list (admin)
    UserDetail.razor         -> User edit + quota (admin / self)
    UsageLog.razor           -> Usage log (admin)
  Shared/
    MainLayout.razor
    NavMenu.razor
    LoginDisplay.razor
    RedirectToLogin.razor
  wwwroot/
    css/
      site.css
```

## Step 4: Configuration

### File: `notebook/frontend/appsettings.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "NotebookApi": {
    "BaseUrl": "http://localhost:3000"
  },
  "AllowedHosts": "*"
}
```

### File: `notebook/frontend/appsettings.Development.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "NotebookApi": {
    "BaseUrl": "http://localhost:3000"
  }
}
```

## Step 5: API Models

### File: `notebook/frontend/Models/ApiModels.cs` (NEW)

```csharp
namespace NotebookFrontend.Models;

// ============================================================================
// Auth
// ============================================================================

public class LoginRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public class LoginResponse
{
    public string Token { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Username { get; set; } = "";
    public string Role { get; set; } = "";
    public int ExpiresInHours { get; set; }
}

public class MeResponse
{
    public string UserId { get; set; } = "";
    public string Username { get; set; } = "";
    public string? DisplayName { get; set; }
    public string Role { get; set; } = "";
    public string AuthorId { get; set; } = "";
}

// ============================================================================
// Notebooks
// ============================================================================

public class NotebookSummary
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Owner { get; set; } = "";
    public bool IsOwner { get; set; }
    public NotebookPermissions Permissions { get; set; } = new();
    public long TotalEntries { get; set; }
    public double TotalEntropy { get; set; }
    public long LastActivitySequence { get; set; }
    public long ParticipantCount { get; set; }
}

public class NotebookPermissions
{
    public bool Read { get; set; }
    public bool Write { get; set; }
}

public class NotebooksListResponse
{
    public List<NotebookSummary> Notebooks { get; set; } = new();
}

public class CreateNotebookRequest
{
    public string Name { get; set; } = "";
}

public class CreateNotebookResponse
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Owner { get; set; } = "";
    public DateTime Created { get; set; }
}

// ============================================================================
// Users
// ============================================================================

public class UserResponse
{
    public string Id { get; set; } = "";
    public string Username { get; set; } = "";
    public string? DisplayName { get; set; }
    public string Role { get; set; } = "";
    public string AuthorId { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime Created { get; set; }
    public DateTime Updated { get; set; }
}

public class UsersListResponse
{
    public List<UserResponse> Users { get; set; } = new();
}

public class CreateUserRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string? DisplayName { get; set; }
    public string? Role { get; set; }
}

public class UpdateUserRequest
{
    public string? DisplayName { get; set; }
    public string? Role { get; set; }
}

// ============================================================================
// Quotas
// ============================================================================

public class QuotaResponse
{
    public string UserId { get; set; } = "";
    public int MaxNotebooks { get; set; }
    public int MaxEntriesPerNotebook { get; set; }
    public int MaxEntrySizeBytes { get; set; }
    public long MaxTotalStorageBytes { get; set; }
}

public class UpdateQuotaRequest
{
    public int? MaxNotebooks { get; set; }
    public int? MaxEntriesPerNotebook { get; set; }
    public int? MaxEntrySizeBytes { get; set; }
    public long? MaxTotalStorageBytes { get; set; }
}

// ============================================================================
// Participants
// ============================================================================

public class Participant
{
    public string AuthorId { get; set; } = "";
    public ParticipantPermissions Permissions { get; set; } = new();
    public DateTime GrantedAt { get; set; }
}

public class ParticipantPermissions
{
    public bool Read { get; set; }
    public bool Write { get; set; }
}

public class ParticipantsResponse
{
    public List<Participant> Participants { get; set; } = new();
}

public class ShareRequest
{
    public string AuthorId { get; set; } = "";
    public ParticipantPermissions Permissions { get; set; } = new();
}

// ============================================================================
// Usage Log
// ============================================================================

public class UsageLogEntry
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    public string AuthorId { get; set; } = "";
    public string Action { get; set; } = "";
    public string? ResourceType { get; set; }
    public string? ResourceId { get; set; }
    public object? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTime Created { get; set; }
}

public class UsageLogResponse
{
    public List<UsageLogEntry> Entries { get; set; } = new();
}

// ============================================================================
// Change Password
// ============================================================================

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
}
```

## Step 6: API Client Service

### File: `notebook/frontend/Services/NotebookApiClient.cs` (NEW)

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NotebookFrontend.Models;

namespace NotebookFrontend.Services;

public class NotebookApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private string? _token;

    public NotebookApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };
    }

    public void SetToken(string? token)
    {
        _token = token;
        if (token != null)
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        else
            _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    public bool HasToken => !string.IsNullOrEmpty(_token);

    // ========================================================================
    // Auth
    // ========================================================================

    public async Task<LoginResponse?> LoginAsync(string username, string password)
    {
        var request = new LoginRequest { Username = username, Password = password };
        var response = await _httpClient.PostAsJsonAsync("/api/auth/login", request, _jsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LoginResponse>(_jsonOptions);
    }

    public async Task<MeResponse?> GetMeAsync()
    {
        return await _httpClient.GetFromJsonAsync<MeResponse>("/api/auth/me", _jsonOptions);
    }

    public async Task ChangePasswordAsync(string currentPassword, string newPassword)
    {
        var request = new ChangePasswordRequest
        {
            CurrentPassword = currentPassword,
            NewPassword = newPassword
        };
        var response = await _httpClient.PostAsJsonAsync("/api/auth/change-password", request, _jsonOptions);
        response.EnsureSuccessStatusCode();
    }

    // ========================================================================
    // Notebooks
    // ========================================================================

    public async Task<List<NotebookSummary>> GetNotebooksAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<NotebooksListResponse>("/notebooks", _jsonOptions);
        return result?.Notebooks ?? new List<NotebookSummary>();
    }

    public async Task<CreateNotebookResponse?> CreateNotebookAsync(string name)
    {
        var request = new CreateNotebookRequest { Name = name };
        var response = await _httpClient.PostAsJsonAsync("/notebooks", request, _jsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CreateNotebookResponse>(_jsonOptions);
    }

    public async Task DeleteNotebookAsync(string id)
    {
        var response = await _httpClient.DeleteAsync($"/notebooks/{id}");
        response.EnsureSuccessStatusCode();
    }

    // ========================================================================
    // Participants
    // ========================================================================

    public async Task<List<Participant>> GetParticipantsAsync(string notebookId)
    {
        var result = await _httpClient.GetFromJsonAsync<ParticipantsResponse>(
            $"/notebooks/{notebookId}/participants", _jsonOptions);
        return result?.Participants ?? new List<Participant>();
    }

    public async Task GrantAccessAsync(string notebookId, string authorId, bool read, bool write)
    {
        var request = new ShareRequest
        {
            AuthorId = authorId,
            Permissions = new ParticipantPermissions { Read = read, Write = write }
        };
        var response = await _httpClient.PostAsJsonAsync(
            $"/notebooks/{notebookId}/share", request, _jsonOptions);
        response.EnsureSuccessStatusCode();
    }

    public async Task RevokeAccessAsync(string notebookId, string authorId)
    {
        var response = await _httpClient.DeleteAsync(
            $"/notebooks/{notebookId}/share/{authorId}");
        response.EnsureSuccessStatusCode();
    }

    // ========================================================================
    // Users
    // ========================================================================

    public async Task<List<UserResponse>> GetUsersAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<UsersListResponse>("/api/users", _jsonOptions);
        return result?.Users ?? new List<UserResponse>();
    }

    public async Task<UserResponse?> GetUserAsync(string id)
    {
        return await _httpClient.GetFromJsonAsync<UserResponse>($"/api/users/{id}", _jsonOptions);
    }

    public async Task<UserResponse?> CreateUserAsync(CreateUserRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/users", request, _jsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserResponse>(_jsonOptions);
    }

    public async Task<UserResponse?> UpdateUserAsync(string id, UpdateUserRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/users/{id}", request, _jsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserResponse>(_jsonOptions);
    }

    public async Task DeactivateUserAsync(string id)
    {
        var response = await _httpClient.DeleteAsync($"/api/users/{id}");
        response.EnsureSuccessStatusCode();
    }

    // ========================================================================
    // Quotas
    // ========================================================================

    public async Task<QuotaResponse?> GetQuotaAsync(string userId)
    {
        return await _httpClient.GetFromJsonAsync<QuotaResponse>(
            $"/api/users/{userId}/quota", _jsonOptions);
    }

    public async Task<QuotaResponse?> UpdateQuotaAsync(string userId, UpdateQuotaRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync(
            $"/api/users/{userId}/quota", request, _jsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<QuotaResponse>(_jsonOptions);
    }

    // ========================================================================
    // Usage Log
    // ========================================================================

    public async Task<List<UsageLogEntry>> GetUsageLogAsync(
        string? userId = null, string? action = null,
        int limit = 100, int offset = 0)
    {
        var query = $"/api/usage?limit={limit}&offset={offset}";
        if (userId != null) query += $"&user_id={userId}";
        if (action != null) query += $"&action={action}";

        var result = await _httpClient.GetFromJsonAsync<UsageLogResponse>(query, _jsonOptions);
        return result?.Entries ?? new List<UsageLogEntry>();
    }

    public async Task<List<UsageLogEntry>> GetNotebookUsageAsync(string notebookId, int limit = 100)
    {
        var result = await _httpClient.GetFromJsonAsync<UsageLogResponse>(
            $"/api/notebooks/{notebookId}/usage?limit={limit}", _jsonOptions);
        return result?.Entries ?? new List<UsageLogEntry>();
    }
}
```

## Step 7: Authentication State Provider

### File: `notebook/frontend/Services/JwtAuthenticationStateProvider.cs` (NEW)

```csharp
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Components.Authorization;

namespace NotebookFrontend.Services;

public class JwtAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly NotebookApiClient _apiClient;
    private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());

    public JwtAuthenticationStateProvider(NotebookApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return Task.FromResult(new AuthenticationState(_currentUser));
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        try
        {
            var response = await _apiClient.LoginAsync(username, password);
            if (response?.Token == null)
                return false;

            _apiClient.SetToken(response.Token);

            // Parse JWT to get claims
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(response.Token);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, response.UserId),
                new(ClaimTypes.Name, response.Username),
                new(ClaimTypes.Role, response.Role),
                new("token", response.Token)
            };

            var identity = new ClaimsIdentity(claims, "jwt");
            _currentUser = new ClaimsPrincipal(identity);

            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Logout()
    {
        _apiClient.SetToken(null);
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public string? GetUserId()
    {
        return _currentUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    public string? GetRole()
    {
        return _currentUser.FindFirst(ClaimTypes.Role)?.Value;
    }

    public bool IsAdmin()
    {
        return GetRole() == "admin";
    }
}
```

## Step 8: Program.cs

### File: `notebook/frontend/Program.cs`

Replace the default content:

```csharp
using Microsoft.AspNetCore.Components.Authorization;
using NotebookFrontend.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Configure HTTP client for notebook API
var apiBaseUrl = builder.Configuration.GetValue<string>("NotebookApi:BaseUrl")
    ?? Environment.GetEnvironmentVariable("NOTEBOOK_API_URL")
    ?? "http://localhost:3000";

builder.Services.AddScoped(sp =>
{
    var client = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
    client.Timeout = TimeSpan.FromSeconds(30);
    return client;
});

builder.Services.AddScoped<NotebookApiClient>();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(
    provider => provider.GetRequiredService<JwtAuthenticationStateProvider>());

builder.Services.AddAuthorizationCore();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
```

## Step 9: Shared Components

### File: `notebook/frontend/Shared/MainLayout.razor`

```razor
@inherits LayoutComponentBase

<div class="page">
    <div class="sidebar">
        <NavMenu />
    </div>

    <main>
        <div class="top-row px-4">
            <LoginDisplay />
        </div>

        <article class="content px-4">
            @Body
        </article>
    </main>
</div>
```

### File: `notebook/frontend/Shared/NavMenu.razor`

```razor
@using Microsoft.AspNetCore.Components.Authorization

<div class="top-row ps-3 navbar navbar-dark">
    <div class="container-fluid">
        <a class="navbar-brand" href="">Notebook Admin</a>
    </div>
</div>

<div class="@NavMenuCssClass nav-scrollable" @onclick="ToggleNavMenu">
    <nav class="flex-column">
        <AuthorizeView>
            <Authorized>
                <div class="nav-item px-3">
                    <NavLink class="nav-link" href="" Match="NavLinkMatch.All">
                        <span class="bi bi-house-door-fill-nav-menu" aria-hidden="true"></span> Dashboard
                    </NavLink>
                </div>

                <AuthorizeView Roles="admin">
                    <div class="nav-item px-3">
                        <NavLink class="nav-link" href="users">
                            <span class="bi bi-people-fill-nav-menu" aria-hidden="true"></span> Users
                        </NavLink>
                    </div>
                    <div class="nav-item px-3">
                        <NavLink class="nav-link" href="usage">
                            <span class="bi bi-list-check-nav-menu" aria-hidden="true"></span> Usage Log
                        </NavLink>
                    </div>
                </AuthorizeView>
            </Authorized>
        </AuthorizeView>
    </nav>
</div>

@code {
    private bool collapseNavMenu = true;
    private string? NavMenuCssClass => collapseNavMenu ? "collapse" : null;

    private void ToggleNavMenu()
    {
        collapseNavMenu = !collapseNavMenu;
    }
}
```

### File: `notebook/frontend/Shared/LoginDisplay.razor` (NEW)

```razor
@using Microsoft.AspNetCore.Components.Authorization
@inject JwtAuthenticationStateProvider AuthStateProvider
@inject NavigationManager Navigation

<AuthorizeView>
    <Authorized>
        <span class="text-light me-3">@context.User.Identity?.Name</span>
        <button class="btn btn-outline-light btn-sm" @onclick="Logout">Logout</button>
    </Authorized>
    <NotAuthorized>
        <a href="login" class="btn btn-outline-light btn-sm">Login</a>
    </NotAuthorized>
</AuthorizeView>

@code {
    private void Logout()
    {
        AuthStateProvider.Logout();
        Navigation.NavigateTo("login");
    }
}
```

### File: `notebook/frontend/Shared/RedirectToLogin.razor` (NEW)

```razor
@inject NavigationManager Navigation

@code {
    protected override void OnInitialized()
    {
        Navigation.NavigateTo("login");
    }
}
```

## Step 10: Pages

### File: `notebook/frontend/Pages/Login.razor` (NEW)

```razor
@page "/login"
@layout MainLayout
@inject JwtAuthenticationStateProvider AuthStateProvider
@inject NavigationManager Navigation

<h3>Login</h3>

@if (!string.IsNullOrEmpty(errorMessage))
{
    <div class="alert alert-danger">@errorMessage</div>
}

<div class="row">
    <div class="col-md-4">
        <EditForm Model="loginModel" OnValidSubmit="HandleLogin">
            <div class="mb-3">
                <label class="form-label">Username</label>
                <InputText class="form-control" @bind-Value="loginModel.Username" />
            </div>
            <div class="mb-3">
                <label class="form-label">Password</label>
                <InputText class="form-control" type="password" @bind-Value="loginModel.Password" />
            </div>
            <button type="submit" class="btn btn-primary" disabled="@isLoading">
                @if (isLoading) { <span>Logging in...</span> } else { <span>Login</span> }
            </button>
        </EditForm>
    </div>
</div>

@code {
    private LoginModel loginModel = new();
    private string? errorMessage;
    private bool isLoading;

    private class LoginModel
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }

    private async Task HandleLogin()
    {
        isLoading = true;
        errorMessage = null;

        try
        {
            var success = await AuthStateProvider.LoginAsync(loginModel.Username, loginModel.Password);
            if (success)
            {
                Navigation.NavigateTo("/");
            }
            else
            {
                errorMessage = "Invalid username or password.";
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Login failed: {ex.Message}";
        }
        finally
        {
            isLoading = false;
        }
    }
}
```

### File: `notebook/frontend/Pages/Index.razor` (Dashboard)

```razor
@page "/"
@using Microsoft.AspNetCore.Authorization
@attribute [Authorize]
@inject NotebookApiClient ApiClient
@inject JwtAuthenticationStateProvider AuthStateProvider

<h3>Notebooks</h3>

<div class="mb-3">
    <div class="input-group" style="max-width: 400px;">
        <input class="form-control" placeholder="New notebook name" @bind="newNotebookName" />
        <button class="btn btn-primary" @onclick="CreateNotebook" disabled="@isCreating">Create</button>
    </div>
</div>

@if (errorMessage != null)
{
    <div class="alert alert-danger">@errorMessage</div>
}

@if (notebooks == null)
{
    <p>Loading...</p>
}
else if (notebooks.Count == 0)
{
    <p>No notebooks yet. Create one above.</p>
}
else
{
    <table class="table table-striped">
        <thead>
            <tr>
                <th>Name</th>
                <th>Owner</th>
                <th>Entries</th>
                <th>Participants</th>
                <th>Entropy</th>
                <th>Actions</th>
            </tr>
        </thead>
        <tbody>
            @foreach (var nb in notebooks)
            {
                <tr>
                    <td><a href="/notebooks/@nb.Id">@nb.Name</a></td>
                    <td>@(nb.IsOwner ? "You" : nb.Owner[..8] + "...")</td>
                    <td>@nb.TotalEntries</td>
                    <td>@nb.ParticipantCount</td>
                    <td>@nb.TotalEntropy.ToString("F2")</td>
                    <td>
                        @if (nb.IsOwner)
                        {
                            <button class="btn btn-danger btn-sm"
                                    @onclick="() => DeleteNotebook(nb.Id)">Delete</button>
                        }
                    </td>
                </tr>
            }
        </tbody>
    </table>
}

@code {
    private List<NotebookSummary>? notebooks;
    private string newNotebookName = "";
    private string? errorMessage;
    private bool isCreating;

    protected override async Task OnInitializedAsync()
    {
        await LoadNotebooks();
    }

    private async Task LoadNotebooks()
    {
        try
        {
            notebooks = await ApiClient.GetNotebooksAsync();
        }
        catch (Exception ex)
        {
            errorMessage = $"Failed to load notebooks: {ex.Message}";
        }
    }

    private async Task CreateNotebook()
    {
        if (string.IsNullOrWhiteSpace(newNotebookName)) return;
        isCreating = true;
        errorMessage = null;
        try
        {
            await ApiClient.CreateNotebookAsync(newNotebookName);
            newNotebookName = "";
            await LoadNotebooks();
        }
        catch (Exception ex)
        {
            errorMessage = $"Failed to create notebook: {ex.Message}";
        }
        finally
        {
            isCreating = false;
        }
    }

    private async Task DeleteNotebook(string id)
    {
        try
        {
            await ApiClient.DeleteNotebookAsync(id);
            await LoadNotebooks();
        }
        catch (Exception ex)
        {
            errorMessage = $"Failed to delete notebook: {ex.Message}";
        }
    }
}
```

### File: `notebook/frontend/Pages/NotebookDetail.razor` (NEW)

```razor
@page "/notebooks/{Id}"
@using Microsoft.AspNetCore.Authorization
@attribute [Authorize]
@inject NotebookApiClient ApiClient

<h3>Notebook Detail</h3>

@if (errorMessage != null)
{
    <div class="alert alert-danger">@errorMessage</div>
}

@if (notebook == null)
{
    <p>Loading...</p>
}
else
{
    <div class="card mb-4">
        <div class="card-body">
            <h5 class="card-title">@notebook.Name</h5>
            <p>Owner: @(notebook.IsOwner ? "You" : notebook.Owner[..16] + "...")</p>
            <p>Entries: @notebook.TotalEntries | Entropy: @notebook.TotalEntropy.ToString("F2") | Participants: @notebook.ParticipantCount</p>
        </div>
    </div>

    <h4>Participants</h4>
    @if (participants != null && participants.Count > 0)
    {
        <table class="table table-sm">
            <thead>
                <tr><th>Author ID</th><th>Read</th><th>Write</th><th>Granted</th><th></th></tr>
            </thead>
            <tbody>
                @foreach (var p in participants)
                {
                    <tr>
                        <td><code>@p.AuthorId[..16]...</code></td>
                        <td>@(p.Permissions.Read ? "Yes" : "No")</td>
                        <td>@(p.Permissions.Write ? "Yes" : "No")</td>
                        <td>@p.GrantedAt.ToString("yyyy-MM-dd HH:mm")</td>
                        <td>
                            @if (notebook.IsOwner && p.AuthorId != notebook.Owner)
                            {
                                <button class="btn btn-sm btn-outline-danger"
                                        @onclick="() => RevokeAccess(p.AuthorId)">Revoke</button>
                            }
                        </td>
                    </tr>
                }
            </tbody>
        </table>
    }
    else
    {
        <p>No participants loaded.</p>
    }

    @if (notebook.IsOwner)
    {
        <h5>Grant Access</h5>
        <div class="input-group mb-3" style="max-width: 600px;">
            <input class="form-control" placeholder="Author ID (64 hex chars)" @bind="grantAuthorId" />
            <button class="btn btn-primary" @onclick="GrantAccess">Grant Read+Write</button>
        </div>
    }

    <h4>Usage Log</h4>
    @if (usageLog != null && usageLog.Count > 0)
    {
        <table class="table table-sm">
            <thead>
                <tr><th>Action</th><th>Author</th><th>Time</th></tr>
            </thead>
            <tbody>
                @foreach (var entry in usageLog)
                {
                    <tr>
                        <td>@entry.Action</td>
                        <td><code>@entry.AuthorId[..16]...</code></td>
                        <td>@entry.Created.ToString("yyyy-MM-dd HH:mm:ss")</td>
                    </tr>
                }
            </tbody>
        </table>
    }
    else
    {
        <p>No usage log entries.</p>
    }
}

@code {
    [Parameter] public string Id { get; set; } = "";

    private NotebookSummary? notebook;
    private List<Participant>? participants;
    private List<UsageLogEntry>? usageLog;
    private string grantAuthorId = "";
    private string? errorMessage;

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private async Task LoadData()
    {
        try
        {
            var notebooks = await ApiClient.GetNotebooksAsync();
            notebook = notebooks.FirstOrDefault(n => n.Id == Id);

            if (notebook != null)
            {
                participants = await ApiClient.GetParticipantsAsync(Id);
                usageLog = await ApiClient.GetNotebookUsageAsync(Id, 50);
            }
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }
    }

    private async Task GrantAccess(string authorId)
    {
        try
        {
            await ApiClient.GrantAccessAsync(Id, grantAuthorId, true, true);
            grantAuthorId = "";
            await LoadData();
        }
        catch (Exception ex) { errorMessage = ex.Message; }
    }

    private async Task RevokeAccess(string authorId)
    {
        try
        {
            await ApiClient.RevokeAccessAsync(Id, authorId);
            await LoadData();
        }
        catch (Exception ex) { errorMessage = ex.Message; }
    }
}
```

### File: `notebook/frontend/Pages/Users.razor` (NEW)

```razor
@page "/users"
@using Microsoft.AspNetCore.Authorization
@attribute [Authorize(Roles = "admin")]
@inject NotebookApiClient ApiClient

<h3>Users</h3>

<div class="mb-3">
    <button class="btn btn-primary" @onclick="() => showCreateForm = !showCreateForm">
        @(showCreateForm ? "Cancel" : "Create User")
    </button>
</div>

@if (showCreateForm)
{
    <div class="card mb-3" style="max-width: 500px;">
        <div class="card-body">
            <div class="mb-2">
                <label class="form-label">Username</label>
                <input class="form-control" @bind="newUsername" />
            </div>
            <div class="mb-2">
                <label class="form-label">Password (min 8 chars)</label>
                <input class="form-control" type="password" @bind="newPassword" />
            </div>
            <div class="mb-2">
                <label class="form-label">Display Name</label>
                <input class="form-control" @bind="newDisplayName" />
            </div>
            <div class="mb-2">
                <label class="form-label">Role</label>
                <select class="form-select" @bind="newRole">
                    <option value="user">User</option>
                    <option value="admin">Admin</option>
                </select>
            </div>
            <button class="btn btn-success" @onclick="CreateUser">Create</button>
        </div>
    </div>
}

@if (errorMessage != null)
{
    <div class="alert alert-danger">@errorMessage</div>
}

@if (users == null)
{
    <p>Loading...</p>
}
else
{
    <table class="table table-striped">
        <thead>
            <tr>
                <th>Username</th>
                <th>Display Name</th>
                <th>Role</th>
                <th>Active</th>
                <th>Created</th>
                <th>Actions</th>
            </tr>
        </thead>
        <tbody>
            @foreach (var u in users)
            {
                <tr class="@(!u.IsActive ? "table-secondary" : "")">
                    <td><a href="/users/@u.Id">@u.Username</a></td>
                    <td>@(u.DisplayName ?? "-")</td>
                    <td><span class="badge @(u.Role == "admin" ? "bg-danger" : "bg-primary")">@u.Role</span></td>
                    <td>@(u.IsActive ? "Yes" : "No")</td>
                    <td>@u.Created.ToString("yyyy-MM-dd")</td>
                    <td>
                        @if (u.IsActive)
                        {
                            <button class="btn btn-sm btn-outline-danger"
                                    @onclick="() => DeactivateUser(u.Id)">Deactivate</button>
                        }
                    </td>
                </tr>
            }
        </tbody>
    </table>
}

@code {
    private List<UserResponse>? users;
    private bool showCreateForm;
    private string newUsername = "";
    private string newPassword = "";
    private string newDisplayName = "";
    private string newRole = "user";
    private string? errorMessage;

    protected override async Task OnInitializedAsync()
    {
        await LoadUsers();
    }

    private async Task LoadUsers()
    {
        try { users = await ApiClient.GetUsersAsync(); }
        catch (Exception ex) { errorMessage = ex.Message; }
    }

    private async Task CreateUser()
    {
        errorMessage = null;
        try
        {
            await ApiClient.CreateUserAsync(new CreateUserRequest
            {
                Username = newUsername,
                Password = newPassword,
                DisplayName = string.IsNullOrWhiteSpace(newDisplayName) ? null : newDisplayName,
                Role = newRole
            });
            showCreateForm = false;
            newUsername = ""; newPassword = ""; newDisplayName = "";
            await LoadUsers();
        }
        catch (Exception ex) { errorMessage = ex.Message; }
    }

    private async Task DeactivateUser(string id)
    {
        try
        {
            await ApiClient.DeactivateUserAsync(id);
            await LoadUsers();
        }
        catch (Exception ex) { errorMessage = ex.Message; }
    }
}
```

### File: `notebook/frontend/Pages/UserDetail.razor` (NEW)

```razor
@page "/users/{Id}"
@using Microsoft.AspNetCore.Authorization
@attribute [Authorize]
@inject NotebookApiClient ApiClient
@inject JwtAuthenticationStateProvider AuthStateProvider

<h3>User Detail</h3>

@if (errorMessage != null)
{
    <div class="alert alert-danger">@errorMessage</div>
}
@if (successMessage != null)
{
    <div class="alert alert-success">@successMessage</div>
}

@if (user == null)
{
    <p>Loading...</p>
}
else
{
    <div class="card mb-4" style="max-width: 600px;">
        <div class="card-body">
            <h5>@user.Username</h5>
            <p>Role: <span class="badge @(user.Role == "admin" ? "bg-danger" : "bg-primary")">@user.Role</span></p>
            <p>Author ID: <code>@user.AuthorId</code></p>
            <p>Active: @(user.IsActive ? "Yes" : "No")</p>
            <p>Created: @user.Created.ToString("yyyy-MM-dd HH:mm")</p>

            <h6>Edit</h6>
            <div class="mb-2">
                <label class="form-label">Display Name</label>
                <input class="form-control" @bind="editDisplayName" />
            </div>
            @if (AuthStateProvider.IsAdmin())
            {
                <div class="mb-2">
                    <label class="form-label">Role</label>
                    <select class="form-select" @bind="editRole">
                        <option value="user">User</option>
                        <option value="admin">Admin</option>
                    </select>
                </div>
            }
            <button class="btn btn-primary" @onclick="UpdateUser">Save</button>
        </div>
    </div>

    @if (AuthStateProvider.IsAdmin() && quota != null)
    {
        <div class="card mb-4" style="max-width: 600px;">
            <div class="card-body">
                <h5>Quota</h5>
                <div class="mb-2">
                    <label class="form-label">Max Notebooks</label>
                    <input class="form-control" type="number" @bind="quota.MaxNotebooks" />
                </div>
                <div class="mb-2">
                    <label class="form-label">Max Entries per Notebook</label>
                    <input class="form-control" type="number" @bind="quota.MaxEntriesPerNotebook" />
                </div>
                <div class="mb-2">
                    <label class="form-label">Max Entry Size (bytes)</label>
                    <input class="form-control" type="number" @bind="quota.MaxEntrySizeBytes" />
                </div>
                <div class="mb-2">
                    <label class="form-label">Max Total Storage (bytes)</label>
                    <input class="form-control" type="number" @bind="quota.MaxTotalStorageBytes" />
                </div>
                <button class="btn btn-primary" @onclick="UpdateQuota">Save Quota</button>
            </div>
        </div>
    }
}

@code {
    [Parameter] public string Id { get; set; } = "";

    private UserResponse? user;
    private QuotaResponse? quota;
    private string editDisplayName = "";
    private string editRole = "user";
    private string? errorMessage;
    private string? successMessage;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            user = await ApiClient.GetUserAsync(Id);
            if (user != null)
            {
                editDisplayName = user.DisplayName ?? "";
                editRole = user.Role;
            }
            if (AuthStateProvider.IsAdmin())
            {
                quota = await ApiClient.GetQuotaAsync(Id);
            }
        }
        catch (Exception ex) { errorMessage = ex.Message; }
    }

    private async Task UpdateUser()
    {
        errorMessage = null; successMessage = null;
        try
        {
            var request = new UpdateUserRequest
            {
                DisplayName = editDisplayName,
                Role = AuthStateProvider.IsAdmin() ? editRole : null
            };
            user = await ApiClient.UpdateUserAsync(Id, request);
            successMessage = "User updated.";
        }
        catch (Exception ex) { errorMessage = ex.Message; }
    }

    private async Task UpdateQuota()
    {
        if (quota == null) return;
        errorMessage = null; successMessage = null;
        try
        {
            var request = new UpdateQuotaRequest
            {
                MaxNotebooks = quota.MaxNotebooks,
                MaxEntriesPerNotebook = quota.MaxEntriesPerNotebook,
                MaxEntrySizeBytes = quota.MaxEntrySizeBytes,
                MaxTotalStorageBytes = quota.MaxTotalStorageBytes
            };
            quota = await ApiClient.UpdateQuotaAsync(Id, request);
            successMessage = "Quota updated.";
        }
        catch (Exception ex) { errorMessage = ex.Message; }
    }
}
```

### File: `notebook/frontend/Pages/UsageLog.razor` (NEW)

```razor
@page "/usage"
@using Microsoft.AspNetCore.Authorization
@attribute [Authorize(Roles = "admin")]
@inject NotebookApiClient ApiClient

<h3>Usage Log</h3>

<div class="mb-3 d-flex gap-2">
    <input class="form-control" style="max-width:200px;" placeholder="Filter by action" @bind="filterAction" />
    <button class="btn btn-primary" @onclick="LoadLog">Filter</button>
    <button class="btn btn-secondary" @onclick="ClearFilter">Clear</button>
</div>

@if (errorMessage != null)
{
    <div class="alert alert-danger">@errorMessage</div>
}

@if (entries == null)
{
    <p>Loading...</p>
}
else if (entries.Count == 0)
{
    <p>No usage log entries.</p>
}
else
{
    <table class="table table-sm table-striped">
        <thead>
            <tr>
                <th>ID</th>
                <th>Action</th>
                <th>User</th>
                <th>Resource</th>
                <th>Time</th>
            </tr>
        </thead>
        <tbody>
            @foreach (var e in entries)
            {
                <tr>
                    <td>@e.Id</td>
                    <td>@e.Action</td>
                    <td>@(e.UserId ?? "-")</td>
                    <td>@(e.ResourceType ?? "-")/@(e.ResourceId ?? "-")</td>
                    <td>@e.Created.ToString("yyyy-MM-dd HH:mm:ss")</td>
                </tr>
            }
        </tbody>
    </table>

    <div class="d-flex gap-2">
        @if (currentOffset > 0)
        {
            <button class="btn btn-sm btn-outline-primary" @onclick="PreviousPage">Previous</button>
        }
        <button class="btn btn-sm btn-outline-primary" @onclick="NextPage">Next</button>
    </div>
}

@code {
    private List<UsageLogEntry>? entries;
    private string? filterAction;
    private string? errorMessage;
    private int currentOffset;
    private const int PageSize = 50;

    protected override async Task OnInitializedAsync()
    {
        await LoadLog();
    }

    private async Task LoadLog()
    {
        try
        {
            entries = await ApiClient.GetUsageLogAsync(
                action: string.IsNullOrWhiteSpace(filterAction) ? null : filterAction,
                limit: PageSize,
                offset: currentOffset);
        }
        catch (Exception ex) { errorMessage = ex.Message; }
    }

    private async Task ClearFilter()
    {
        filterAction = null;
        currentOffset = 0;
        await LoadLog();
    }

    private async Task NextPage()
    {
        currentOffset += PageSize;
        await LoadLog();
    }

    private async Task PreviousPage()
    {
        currentOffset = Math.Max(0, currentOffset - PageSize);
        await LoadLog();
    }
}
```

## Step 11: Imports File

### File: `notebook/frontend/_Imports.razor`

```razor
@using System.Net.Http
@using Microsoft.AspNetCore.Authorization
@using Microsoft.AspNetCore.Components.Authorization
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.AspNetCore.Components.Web.Virtualization
@using Microsoft.JSInterop
@using NotebookFrontend
@using NotebookFrontend.Shared
@using NotebookFrontend.Models
@using NotebookFrontend.Services
```

## Step 12: Dockerfile

### File: `notebook/frontend/Dockerfile` (NEW)

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY *.csproj ./
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out .

EXPOSE 5000

ENV ASPNETCORE_URLS=http://+:5000
ENV NOTEBOOK_API_URL=http://notebook-server:3000

CMD ["dotnet", "NotebookFrontend.dll"]
```

## Verification

1. Build the project:
   ```bash
   cd notebook/frontend
   dotnet build
   ```

2. Run locally (with notebook-server running on port 3000):
   ```bash
   NOTEBOOK_API_URL=http://localhost:3000 dotnet run
   ```

3. Open browser to `http://localhost:5000`
4. Login with admin credentials
5. Verify dashboard shows notebooks
6. Verify Users page shows user list (admin only)
7. Verify Usage Log page shows audit trail
8. Create a notebook via the UI and verify it appears
9. Docker build:
   ```bash
   docker build -t notebook-frontend -f notebook/frontend/Dockerfile notebook/frontend/
   ```

## Files Created Summary

| File | Description |
|------|-------------|
| `notebook/frontend/NotebookFrontend.csproj` | .NET project file (created by `dotnet new`) |
| `notebook/frontend/Program.cs` | App entry point with DI setup |
| `notebook/frontend/appsettings.json` | Configuration |
| `notebook/frontend/appsettings.Development.json` | Dev configuration |
| `notebook/frontend/Dockerfile` | Container build |
| `notebook/frontend/_Imports.razor` | Global Razor imports |
| `notebook/frontend/Models/ApiModels.cs` | API data transfer objects |
| `notebook/frontend/Services/NotebookApiClient.cs` | Typed HTTP client |
| `notebook/frontend/Services/JwtAuthenticationStateProvider.cs` | Blazor auth state |
| `notebook/frontend/Shared/MainLayout.razor` | Page layout |
| `notebook/frontend/Shared/NavMenu.razor` | Navigation menu |
| `notebook/frontend/Shared/LoginDisplay.razor` | Login/logout button |
| `notebook/frontend/Shared/RedirectToLogin.razor` | Auth redirect |
| `notebook/frontend/Pages/Login.razor` | Login page |
| `notebook/frontend/Pages/Index.razor` | Dashboard (notebook list) |
| `notebook/frontend/Pages/NotebookDetail.razor` | Notebook metadata + participants |
| `notebook/frontend/Pages/Users.razor` | User management (admin) |
| `notebook/frontend/Pages/UserDetail.razor` | User edit + quota |
| `notebook/frontend/Pages/UsageLog.razor` | Audit log (admin) |
