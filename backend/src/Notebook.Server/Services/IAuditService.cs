using Notebook.Core.Types;

namespace Notebook.Server.Services;

public interface IAuditService
{
    ValueTask LogAsync(AuditEvent auditEvent);
}
