using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace Notebook.Server.Auth;

/// <summary>
/// ASP.NET Core authentication handler that validates EdDSA (Ed25519) JWT tokens.
/// Mirrors the Rust notebook-server's extract.rs logic.
/// </summary>
public class EdDsaAuthenticationHandler : AuthenticationHandler<EdDsaAuthenticationOptions>
{
    public const string SchemeName = "EdDsa";

    public EdDsaAuthenticationHandler(
        IOptionsMonitor<EdDsaAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Try JWT Bearer token first
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (authHeader is not null && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader["Bearer ".Length..].Trim();
            if (!string.IsNullOrEmpty(token))
            {
                var result = ValidateJwt(token);
                if (result.Succeeded)
                    return Task.FromResult(result);

                // JWT failed — fall through to dev identity if allowed
                if (!Options.AllowDevIdentity)
                    return Task.FromResult(result);
            }
        }

        // Fall back to dev identity (X-Author-Id header or zero author)
        if (Options.AllowDevIdentity)
        {
            return Task.FromResult(HandleDevIdentity());
        }

        return Task.FromResult(AuthenticateResult.Fail("Missing Authorization: Bearer <jwt> header"));
    }

    private AuthenticateResult ValidateJwt(string token)
    {
        if (Options.PublicKey is null)
            return AuthenticateResult.Fail("JWT public key not configured on server");

        var parts = token.Split('.');
        if (parts.Length != 3)
            return AuthenticateResult.Fail("Invalid JWT format");

        try
        {
            // Verify signature
            var messageBytes = Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}");
            var signatureBytes = Base64UrlDecode(parts[2]);

            var verifier = new Ed25519Signer();
            verifier.Init(false, Options.PublicKey);
            verifier.BlockUpdate(messageBytes, 0, messageBytes.Length);

            if (!verifier.VerifySignature(signatureBytes))
                return AuthenticateResult.Fail("Invalid token signature");

            // Decode payload
            var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            var payload = JsonSerializer.Deserialize<JwtPayload>(payloadJson);

            if (payload is null)
                return AuthenticateResult.Fail("Invalid token payload");

            // Validate issuer
            if (payload.iss != "notebook-admin")
                return AuthenticateResult.Fail($"Invalid issuer: {payload.iss}");

            // Validate expiration
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (payload.exp <= now)
                return AuthenticateResult.Fail("Token expired");

            // Validate not-before
            if (payload.nbf > now + 60) // 60s clock skew tolerance
                return AuthenticateResult.Fail("Token not yet valid");

            // Build claims principal
            var claims = new List<Claim>
            {
                new("sub", payload.sub ?? ""),
            };

            if (payload.iss is not null)
                claims.Add(new Claim("iss", payload.iss));

            if (payload.scope is not null)
            {
                foreach (var scope in payload.scope.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    claims.Add(new Claim("scope", scope));
            }

            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);

            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "JWT validation failed");
            return AuthenticateResult.Fail($"Invalid token: {ex.Message}");
        }
    }

    private AuthenticateResult HandleDevIdentity()
    {
        string authorHex;

        if (Request.Headers.TryGetValue("X-Author-Id", out var devHeader) &&
            !string.IsNullOrEmpty(devHeader.FirstOrDefault()))
        {
            authorHex = devHeader.First()!;
            Logger.LogDebug("Using dev identity from X-Author-Id header: {AuthorId}", authorHex);
        }
        else
        {
            // Zero author — same as Rust server dev fallback
            authorHex = new string('0', 64);
            Logger.LogWarning("No auth provided, using zero author (dev mode)");
        }

        var claims = new List<Claim>
        {
            new("sub", authorHex),
            new("scope", "notebook:read"),
            new("scope", "notebook:write"),
            new("scope", "notebook:share"),
            new("scope", "notebook:admin"),
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return AuthenticateResult.Success(ticket);
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var output = input.Replace('-', '+').Replace('_', '/');
        switch (output.Length % 4)
        {
            case 2: output += "=="; break;
            case 3: output += "="; break;
        }
        return Convert.FromBase64String(output);
    }

    private sealed class JwtPayload
    {
        public string? sub { get; set; }
        public string? iss { get; set; }
        public long exp { get; set; }
        public long nbf { get; set; }
        public long iat { get; set; }
        public string? scope { get; set; }
    }
}

public class EdDsaAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// Ed25519 public key for JWT signature verification.
    /// Parsed from Base64-encoded SPKI (SubjectPublicKeyInfo) format.
    /// </summary>
    public Ed25519PublicKeyParameters? PublicKey { get; set; }

    /// <summary>
    /// Allow X-Author-Id header fallback for dev mode.
    /// When true, requests without a JWT Bearer token can use X-Author-Id
    /// or fall through to a zero-author identity.
    /// </summary>
    public bool AllowDevIdentity { get; set; }
}
