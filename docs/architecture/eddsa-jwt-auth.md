# EdDSA JWT Authentication for Notebook.Server

**Date**: 2026-02-15
**Status**: Implemented
**Affects**: `thinktank/src/Notebook.Server/`

## Problem

The .NET Notebook.Server returned HTTP 401 Unauthorized for all requests, including authenticated ones from the Rust CLI with a valid JWT token.

```
$ ./target/debug/notebook --url http://localhost:5281 create uno
Error: Server error (401):
```

## Root Cause

`Program.cs` called `.AddJwtBearer()` with **zero configuration**:

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();  // no signing key, no issuer — rejects everything
```

ASP.NET Core's built-in JWT Bearer middleware requires at minimum an `IssuerSigningKey` to validate tokens. Without it, every token fails validation and the middleware returns 401. Additionally, the built-in handler does not support the EdDSA (Ed25519) algorithm used by our token issuer.

## Token Flow

```
admin app (TokenService.cs)
  ├── Signs JWT with Ed25519 private key via BouncyCastle
  ├── Algorithm: EdDSA
  ├── Issuer: "notebook-admin"
  ├── Claims: sub (AuthorId hex), scope, exp, nbf, iat
  └── Key pair in admin/appsettings.Development.json

Rust CLI (notebook/)
  ├── Reads NOTEBOOK_TOKEN env var
  └── Sends Authorization: Bearer <token>

Rust Server (notebook-server)
  ├── Validates EdDSA signature via jsonwebtoken crate
  ├── Checks issuer = "notebook-admin", exp, nbf
  ├── Extracts sub → AuthorId
  └── Dev fallback: X-Author-Id header when ALLOW_DEV_IDENTITY=true

.NET Server (Notebook.Server) ← WAS BROKEN
  ├── Had .AddJwtBearer() with no config
  └── All endpoints call httpContext.User.FindFirst("sub")?.Value
```

## Solution

Replaced the empty JWT Bearer middleware with a custom `EdDsaAuthenticationHandler` that mirrors the Rust server's `extract.rs` logic.

### Files Changed

| File | Change |
|------|--------|
| `Notebook.Server.csproj` | Added `BouncyCastle.Cryptography 2.6.2` |
| `Auth/EdDsaAuthenticationHandler.cs` | New custom authentication handler |
| `appsettings.Development.json` | Added `Jwt:PublicKey` and `AllowDevIdentity` |
| `Program.cs` | Wired up custom auth scheme |

### Authentication Handler

The custom `EdDsaAuthenticationHandler` implements `AuthenticationHandler<EdDsaAuthenticationOptions>` and:

1. Extracts the Bearer token from the `Authorization` header
2. Splits the JWT into header/payload/signature parts
3. Verifies the Ed25519 signature using BouncyCastle's `Ed25519Signer`
4. Validates issuer (`notebook-admin`), expiration, and not-before claims
5. Sets `sub`, `iss`, and `scope` claims on the `ClaimsPrincipal`
6. Falls back to `X-Author-Id` header (dev mode) when `AllowDevIdentity = true`

### Configuration

**appsettings.Development.json**:
```json
{
  "Jwt": {
    "PublicKey": "MCowBQYDK2VwAyEAF77yKVNJ+mfeSoEm43HP2z+/upKP2Od7DYjiWhJxNjA="
  },
  "AllowDevIdentity": true
}
```

The public key is the SPKI-encoded Ed25519 public key matching the admin app's private key. For production, `AllowDevIdentity` should be `false`.

## Parity with Rust Server

| Feature | Rust Server | .NET Server |
|---------|-------------|-------------|
| EdDSA JWT validation | `jsonwebtoken` crate | BouncyCastle `Ed25519Signer` |
| Issuer check | `"notebook-admin"` | `"notebook-admin"` |
| Expiry/NBF validation | Yes | Yes (60s clock skew tolerance) |
| Dev identity fallback | `X-Author-Id` / zero author | `X-Author-Id` / zero author |
| Scope extraction | `scope` claim → Vec | `scope` claim → multiple Claims |
| Config source | Env vars (`JWT_PUBLIC_KEY`) | appsettings (`Jwt:PublicKey`) |
