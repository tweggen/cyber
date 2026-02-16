using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Notebook.Data;
using Npgsql;

namespace Notebook.Tests;

/// <summary>
/// Shared test fixture that creates a temporary PostgreSQL test database and boots
/// Notebook.Server via WebApplicationFactory. The test database is dropped on disposal.
/// </summary>
public class NotebookApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Connect to the same PostgreSQL instance used for development, but with a separate database.
    private const string Host = "localhost";
    private const int Port = 5432;
    private const string Username = "postgres";
    private const string Password = "admin";

    private readonly string _testDb = $"thinktank_test_{Guid.NewGuid():N}";

    private string TestConnectionString =>
        $"Host={Host};Port={Port};Database={_testDb};Username={Username};Password={Password}";

    private string AdminConnectionString =>
        $"Host={Host};Port={Port};Database=postgres;Username={Username};Password={Password}";

    public async Task InitializeAsync()
    {
        await using var conn = new NpgsqlConnection(AdminConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE \"{_testDb}\"";
        await cmd.ExecuteNonQueryAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        // Drop the test database
        await using var conn = new NpgsqlConnection(AdminConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        // Terminate any remaining connections first
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

        builder.ConfigureServices(services =>
        {
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NotebookDbContext>();
            db.Database.EnsureCreated();
        });
    }
}
