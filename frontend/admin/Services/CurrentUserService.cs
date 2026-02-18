using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using NotebookAdmin.Models;

namespace NotebookAdmin.Services;

/// <summary>
/// Resolves the current authenticated user's AuthorIdHex from the ClaimsPrincipal.
/// </summary>
public class CurrentUserService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public CurrentUserService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    /// <summary>
    /// Get the AuthorIdHex for the authenticated user, or null if not authenticated.
    /// </summary>
    public async Task<string?> GetAuthorIdHexAsync(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return null;

        var appUser = await _userManager.GetUserAsync(user);
        return appUser?.AuthorIdHex;
    }

    /// <summary>
    /// Get the ApplicationUser for the authenticated user, or null if not authenticated.
    /// </summary>
    public async Task<ApplicationUser?> GetCurrentUserAsync(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return null;

        return await _userManager.GetUserAsync(user);
    }
}
