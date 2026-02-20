using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Notebook.Data;
using Notebook.Data.Entities;

namespace Notebook.Server.Services;

public sealed class AuditService : BackgroundService, IAuditService
{
    private readonly Channel<AuditEntry> _channel = Channel.CreateBounded<AuditEntry>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest });

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditService> _logger;

    public AuditService(IServiceScopeFactory scopeFactory, ILogger<AuditService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void Log(byte[] actor, string action, string resource, object? detail = null, string? ip = null, string? userAgent = null)
    {
        var entry = new AuditEntry
        {
            Actor = actor,
            Action = action,
            Resource = resource,
            DetailJson = detail is not null ? JsonSerializer.Serialize(detail) : null,
            Ip = ip,
            UserAgent = userAgent,
        };

        if (!_channel.Writer.TryWrite(entry))
        {
            _logger.LogWarning("Audit channel full, dropping oldest entry");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<AuditEntry>(50);

        await foreach (var entry in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            batch.Add(entry);

            // Drain any additional queued entries up to batch size
            while (batch.Count < 50 && _channel.Reader.TryRead(out var extra))
                batch.Add(extra);

            try
            {
                await WriteBatchAsync(batch, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write {Count} audit entries", batch.Count);
            }

            batch.Clear();
        }
    }

    private async Task WriteBatchAsync(List<AuditEntry> batch, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotebookDbContext>();

        foreach (var entry in batch)
        {
            db.AuditLog.Add(new AuditLogEntity
            {
                Id = Guid.NewGuid(),
                Actor = entry.Actor,
                Action = entry.Action,
                Resource = entry.Resource,
                Detail = entry.DetailJson,
                Ip = entry.Ip,
                UserAgent = entry.UserAgent,
                Created = DateTimeOffset.UtcNow,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private sealed class AuditEntry
    {
        public byte[] Actor { get; init; } = null!;
        public string Action { get; init; } = null!;
        public string Resource { get; init; } = null!;
        public string? DetailJson { get; init; }
        public string? Ip { get; init; }
        public string? UserAgent { get; init; }
    }
}
