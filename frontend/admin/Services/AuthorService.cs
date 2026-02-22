using System.Security.Cryptography;
using NotebookAdmin.Models;

namespace NotebookAdmin.Services;

/// <summary>
/// Manages author registration via the notebook API during user creation.
/// </summary>
public class AuthorService
{
    private readonly NotebookApiClient _apiClient;
    private readonly ILogger<AuthorService> _logger;

    public AuthorService(NotebookApiClient apiClient, ILogger<AuthorService> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    /// <summary>
    /// Generate a new Ed25519-like keypair and register it with the notebook API.
    /// Returns the AuthorId (hex) and public key bytes.
    /// </summary>
    /// <remarks>
    /// For simplicity, we generate a random 32-byte "public key" and let the notebook API
    /// compute the AuthorId as BLAKE3(public_key). In production, this should use
    /// proper Ed25519 key generation.
    /// </remarks>
    public async Task<(string AuthorIdHex, byte[] PublicKey)> RegisterNewAuthorAsync()
    {
        // Generate a random 32-byte key (placeholder for Ed25519 public key)
        var publicKey = RandomNumberGenerator.GetBytes(32);
        var publicKeyHex = Convert.ToHexString(publicKey).ToLowerInvariant();

        _logger.LogInformation("Registering new author with public key {PublicKeyPrefix}...",
            publicKeyHex[..16]);

        var result = await _apiClient.RegisterAuthorAsync(publicKeyHex);
        if (result == null)
        {
            throw new InvalidOperationException("Failed to register author with notebook API");
        }

        _logger.LogInformation("Author registered with ID {AuthorId}", result.AuthorId);

        var authorIdBytes = Convert.FromHexString(result.AuthorId);
        return (result.AuthorId, authorIdBytes);
    }
}
