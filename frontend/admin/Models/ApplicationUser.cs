using Microsoft.AspNetCore.Identity;

namespace NotebookAdmin.Models;

/// <summary>
/// Application user extending ASP.NET Core Identity with notebook AuthorId.
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>
    /// Crypto identity in the notebook system (BLAKE3 hash of public key, 32 bytes).
    /// </summary>
    public byte[] AuthorId { get; set; } = new byte[32];

    /// <summary>
    /// Hex representation of AuthorId for convenience.
    /// </summary>
    public string AuthorIdHex { get; set; } = new string('0', 64);

    /// <summary>
    /// Display name for the user.
    /// </summary>
    public string? DisplayName { get; set; }
}
