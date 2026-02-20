using System.Text.Json;
using Notebook.Core.Types;
using Npgsql;

namespace Notebook.Server.Services;

public class AuditConsumerService(
    AuditService auditService,
    IConfiguration configuration,
    ILogger<AuditConsumerService> logger) : BackgroundService
{
    private const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = auditService.Reader;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var batch = new List<AuditEvent>(BatchSize);

                // Wait for at least one event
                if (await reader.WaitToReadAsync(stoppingToken))
                {
                    while (batch.Count < BatchSize && reader.TryRead(out var evt))
                        batch.Add(evt);
                }

                if (batch.Count > 0)
                    await FlushBatchAsync(batch, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Audit consumer error");
                await Task.Delay(1000, stoppingToken);
            }
        }

        // Drain remaining events on shutdown
        await DrainAsync();
    }

    private async Task DrainAsync()
    {
        var reader = auditService.Reader;
        var batch = new List<AuditEvent>(BatchSize);

        while (reader.TryRead(out var evt))
        {
            batch.Add(evt);
            if (batch.Count >= BatchSize)
            {
                await FlushBatchAsync(batch, CancellationToken.None);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
            await FlushBatchAsync(batch, CancellationToken.None);
    }

    private async Task FlushBatchAsync(List<AuditEvent> batch, CancellationToken ct)
    {
        try
        {
            var connectionString = configuration.GetConnectionString("Notebook");
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();

            var values = new List<string>(batch.Count);
            var paramIndex = 0;

            foreach (var evt in batch)
            {
                var pNotebookId = $"@p{paramIndex++}";
                var pAuthorId = $"@p{paramIndex++}";
                var pAction = $"@p{paramIndex++}";
                var pTargetType = $"@p{paramIndex++}";
                var pTargetId = $"@p{paramIndex++}";
                var pDetail = $"@p{paramIndex++}";
                var pIpAddress = $"@p{paramIndex++}";
                var pUserAgent = $"@p{paramIndex++}";

                values.Add(
                    $"({pNotebookId}, {pAuthorId}, {pAction}, {pTargetType}, {pTargetId}, {pDetail}::jsonb, {pIpAddress}::inet, {pUserAgent})");

                cmd.Parameters.AddWithValue(pNotebookId, (object?)evt.NotebookId ?? DBNull.Value);
                cmd.Parameters.AddWithValue(pAuthorId, (object?)evt.AuthorId ?? DBNull.Value);
                cmd.Parameters.AddWithValue(pAction, evt.Action);
                cmd.Parameters.AddWithValue(pTargetType, (object?)evt.TargetType ?? DBNull.Value);
                cmd.Parameters.AddWithValue(pTargetId, (object?)evt.TargetId ?? DBNull.Value);
                cmd.Parameters.AddWithValue(pDetail,
                    evt.Detail.HasValue ? evt.Detail.Value.GetRawText() : DBNull.Value);
                cmd.Parameters.AddWithValue(pIpAddress, (object?)evt.IpAddress ?? DBNull.Value);
                cmd.Parameters.AddWithValue(pUserAgent, (object?)evt.UserAgent ?? DBNull.Value);
            }

            cmd.CommandText =
                $"""
                 INSERT INTO audit_log (notebook_id, author_id, action, target_type, target_id, detail, ip_address, user_agent)
                 VALUES {string.Join(",\n", values)}
                 """;

            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to flush {Count} audit events to database, writing to overflow file", batch.Count);
            await WriteOverflowAsync(batch);
        }
    }

    private static async Task WriteOverflowAsync(List<AuditEvent> batch)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "audit-overflow");
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, $"audit-overflow-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.jsonl");
        var lines = batch.Select(e => JsonSerializer.Serialize(e));
        await File.WriteAllLinesAsync(path, lines);
    }
}
