using System.Text;
using System.Text.Json;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace NotebookAdmin.Services;

/// <summary>
/// Issues EdDSA-signed JWTs for authenticating with the Rust notebook API.
/// Uses Ed25519 via BouncyCastle for signing.
/// </summary>
public class TokenService
{
    private readonly Ed25519PrivateKeyParameters _privateKey;
    private readonly string _issuer;
    private readonly int _expiryMinutes;
    private readonly ILogger<TokenService> _logger;

    public TokenService(IConfiguration configuration, ILogger<TokenService> logger)
    {
        _logger = logger;
        _issuer = configuration["Jwt:Issuer"] ?? "notebook-admin";
        _expiryMinutes = int.TryParse(configuration["Jwt:ExpiryMinutes"], out var exp) ? exp : 60;

        var privateKeyBase64 = configuration["Jwt:PrivateKey"]
            ?? throw new InvalidOperationException("Jwt:PrivateKey not configured");

        var privateKeyBytes = Convert.FromBase64String(privateKeyBase64);
        _privateKey = ParseEd25519PrivateKey(privateKeyBytes);

        _logger.LogInformation("TokenService initialized with issuer={Issuer}, expiry={ExpiryMinutes}m",
            _issuer, _expiryMinutes);
    }

    /// <summary>
    /// Generate a signed JWT for the given AuthorId.
    /// </summary>
    public string GenerateToken(string authorIdHex)
    {
        var now = DateTimeOffset.UtcNow;
        var exp = now.AddMinutes(_expiryMinutes);

        var header = new { alg = "EdDSA", typ = "JWT" };
        var payload = new
        {
            sub = authorIdHex,
            iss = _issuer,
            exp = exp.ToUnixTimeSeconds(),
            nbf = now.ToUnixTimeSeconds(),
            iat = now.ToUnixTimeSeconds(),
            scope = "notebook:read notebook:write",
        };

        var headerJson = JsonSerializer.Serialize(header);
        var payloadJson = JsonSerializer.Serialize(payload);

        var headerB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
        var payloadB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));

        var message = $"{headerB64}.{payloadB64}";
        var messageBytes = Encoding.UTF8.GetBytes(message);

        var signer = new Ed25519Signer();
        signer.Init(true, _privateKey);
        signer.BlockUpdate(messageBytes, 0, messageBytes.Length);
        var signature = signer.GenerateSignature();

        var signatureB64 = Base64UrlEncode(signature);
        return $"{headerB64}.{payloadB64}.{signatureB64}";
    }

    /// <summary>
    /// Parse a PKCS#8-encoded Ed25519 private key (as produced by openssl genpkey).
    /// PKCS#8 DER for Ed25519: 16-byte prefix + 32-byte raw private key.
    /// </summary>
    private static Ed25519PrivateKeyParameters ParseEd25519PrivateKey(byte[] pkcs8Der)
    {
        // PKCS#8 wrapper for Ed25519 is 48 bytes: 16-byte ASN.1 prefix + 32-byte key
        if (pkcs8Der.Length == 48)
        {
            var rawKey = new byte[32];
            Array.Copy(pkcs8Der, 16, rawKey, 0, 32);
            return new Ed25519PrivateKeyParameters(rawKey, 0);
        }

        // Raw 32-byte key
        if (pkcs8Der.Length == 32)
        {
            return new Ed25519PrivateKeyParameters(pkcs8Der, 0);
        }

        throw new ArgumentException(
            $"Unexpected Ed25519 private key length: {pkcs8Der.Length} bytes. Expected 48 (PKCS#8) or 32 (raw).");
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
