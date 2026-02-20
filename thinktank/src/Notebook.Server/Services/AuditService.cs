using System.Threading.Channels;
using Notebook.Core.Types;

namespace Notebook.Server.Services;

public class AuditService : IAuditService
{
    private readonly Channel<AuditEvent> _channel = Channel.CreateBounded<AuditEvent>(
        new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });

    internal ChannelReader<AuditEvent> Reader => _channel.Reader;

    public ValueTask LogAsync(AuditEvent auditEvent)
    {
        return _channel.Writer.WriteAsync(auditEvent);
    }
}
