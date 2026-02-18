using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Notebook.Data;
using Npgsql;

namespace Notebook.Tests;

/// <summary>
/// Shared test fixture that creates a temporary PostgreSQL test database, applies
/// the real SQL migrations (from notebook/migrations/), and boots Notebook.Server
/// via WebApplicationFactory. The test database is dropped on disposal.
/// </summary>
public class NotebookApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string Host = "localhost";
    private const int Port = 5432;
    private const string Username = "postgres";
    private const string Password = "admin";

    /// <summary>
    /// Migrations to run in order. These are the same SQL files used by the Rust server,
    /// minus init.sql and 003_graph.sql which require Apache AGE.
    /// </summary>
    private static readonly string[] Migrations =
    [
        "002_schema.sql",
        "004_coherence_links.sql",
        "006_notebook_sequence.sql",
        "007_claims_and_jobs.sql",
        "008_original_content_type.sql",
        "009_embeddings.sql",
        "010_job_priority.sql",
        "011_add_source_column.sql",
        "012_integration_status.sql",
    ];

    private readonly string _testDb = $"thinktank_test_{Guid.NewGuid():N}";

    private string TestConnectionString =>
        $"Host={Host};Port={Port};Database={_testDb};Username={Username};Password={Password}";

    private string AdminConnectionString =>
        $"Host={Host};Port={Port};Database=postgres;Username={Username};Password={Password}";

    public async Task InitializeAsync()
    {
        // Create the test database
        await using var conn = new NpgsqlConnection(AdminConnectionString);
        await conn.OpenAsync();
        await using var createCmd = conn.CreateCommand();
        createCmd.CommandText = $"CREATE DATABASE \"{_testDb}\"";
        await createCmd.ExecuteNonQueryAsync();

        // Apply the real SQL migrations
        await using var testConn = new NpgsqlConnection(TestConnectionString);
        await testConn.OpenAsync();

        var migrationsDir = Path.Combine(AppContext.BaseDirectory, "Migrations");
        foreach (var file in Migrations)
        {
            var sql = await File.ReadAllTextAsync(Path.Combine(migrationsDir, file));
            await using var cmd = testConn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await using var conn = new NpgsqlConnection(AdminConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT pg_terminate_backend(pid)
            FROM pg_stat_activity
            WHERE datname = '{_testDb}' AND pid <> pg_backend_pid()
            """;
        await cmd.ExecuteNonQueryAsync();
        cmd.CommandText = $"DROP DATABASE IF EXISTS \"{_testDb}\"";
        await cmd.ExecuteNonQueryAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:Notebook", TestConnectionString);
        builder.UseSetting("AllowDevIdentity", "true");
    }
}
