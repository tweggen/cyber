using System.Text.Json;
using Notebook.Core.Types;
using Npgsql;

namespace Notebook.Server.Services;

public class AuditRecoveryService(
    IConfiguration configuration,
    ILogger<AuditRecoveryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait 5 seconds after startup before attempting recovery
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        var dir = Path.Combine(AppContext.BaseDirectory, "audit-overflow");
        if (!Directory.Exists(dir))
            return;

        var files = Directory.GetFiles(dir, "*.jsonl");
        if (files.Length == 0)
            return;

        logger.LogInformation("Found {Count} audit overflow files to replay", files.Length);

        var connectionString = configuration.GetConnectionString("Notebook");

        foreach (var file in files)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            try
            {
                var lines = await File.ReadAllLinesAsync(file, stoppingToken);
                var events = lines
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Select(l => JsonSerializer.Deserialize<AuditEvent>(l)!)
                    .ToList();

                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync(stoppingToken);

                foreach (var evt in events)
                {
                    await using var cmd = conn.CreateCommand();
                    cmd.CommandText =
                        """
                        INSERT INTO audit_log (notebook_id, author_id, action, target_type, target_id, detail, ip_address, user_agent)
                        VALUES (@notebook_id, @author_id, @action, @target_type, @target_id, @detail::jsonb, @ip_address::inet, @user_agent)
                        """;

                    cmd.Parameters.AddWithValue("notebook_id", (object?)evt.NotebookId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("author_id", (object?)evt.AuthorId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("action", evt.Action);
                    cmd.Parameters.AddWithValue("target_type", (object?)evt.TargetType ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("target_id", (object?)evt.TargetId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("detail",
                        evt.Detail.HasValue ? evt.Detail.Value.GetRawText() : DBNull.Value);
                    cmd.Parameters.AddWithValue("ip_address", (object?)evt.IpAddress ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("user_agent", (object?)evt.UserAgent ?? DBNull.Value);

                    await cmd.ExecuteNonQueryAsync(stoppingToken);
                }

                File.Delete(file);
                logger.LogInformation("Replayed and deleted overflow file {File}", Path.GetFileName(file));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to replay overflow file {File}", Path.GetFileName(file));
            }
        }
    }
}
