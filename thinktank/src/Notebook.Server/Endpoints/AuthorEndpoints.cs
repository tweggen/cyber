using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notebook.Data;
using Notebook.Server.Models;
using Org.BouncyCastle.Crypto.Digests;

namespace Notebook.Server.Endpoints;

public static class AuthorEndpoints
{
    public static void MapAuthorEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/authors", RegisterAuthor);
        routes.MapGet("/authors/{authorIdHex}", GetAuthor);
    }

    private static async Task<IResult> RegisterAuthor(
        [FromBody] RegisterAuthorRequest request,
        NotebookDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.PublicKey) || request.PublicKey.Length != 64)
            return Results.BadRequest(new { error = "public_key must be 64 hex characters" });

        byte[] publicKey;
        try
        {
            publicKey = Convert.FromHexString(request.PublicKey);
        }
        catch (FormatException)
        {
            return Results.BadRequest(new { error = "Invalid hex in public_key" });
        }

        // Compute AuthorId as BLAKE3(public_key), matching the Rust server
        var authorId = ComputeBlake3(publicKey);

        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO authors (id, public_key)
            VALUES (@id, @key)
            ON CONFLICT (id) DO NOTHING
            """;

        var pId = cmd.CreateParameter();
        pId.ParameterName = "id";
        pId.Value = authorId;
        cmd.Parameters.Add(pId);

        var pKey = cmd.CreateParameter();
        pKey.ParameterName = "key";
        pKey.Value = publicKey;
        cmd.Parameters.Add(pKey);

        await cmd.ExecuteNonQueryAsync(ct);

        var authorIdHex = Convert.ToHexString(authorId).ToLowerInvariant();
        return Results.Created($"/authors/{authorIdHex}", new RegisterAuthorResponse
        {
            AuthorId = authorIdHex,
        });
    }

    private static async Task<IResult> GetAuthor(
        string authorIdHex,
        NotebookDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(authorIdHex) || authorIdHex.Length != 64)
            return Results.BadRequest(new { error = "author_id must be 64 hex characters" });

        byte[] authorId;
        try
        {
            authorId = Convert.FromHexString(authorIdHex);
        }
        catch (FormatException)
        {
            return Results.BadRequest(new { error = "Invalid hex in author_id" });
        }

        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT public_key, created FROM authors WHERE id = @id";

        var pId = cmd.CreateParameter();
        pId.ParameterName = "id";
        pId.Value = authorId;
        cmd.Parameters.Add(pId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return Results.NotFound(new { error = $"Author {authorIdHex} not found" });

        var publicKey = (byte[])reader["public_key"];
        var created = reader.GetDateTime(reader.GetOrdinal("created"));

        return Results.Ok(new AuthorResponse
        {
            AuthorId = authorIdHex.ToLowerInvariant(),
            PublicKey = Convert.ToHexString(publicKey).ToLowerInvariant(),
            Created = created.ToString("o"),
        });
    }

    private static byte[] ComputeBlake3(byte[] input)
    {
        var digest = new Blake3Digest(32);
        digest.BlockUpdate(input, 0, input.Length);
        var output = new byte[32];
        digest.DoFinal(output, 0);
        return output;
    }
}
