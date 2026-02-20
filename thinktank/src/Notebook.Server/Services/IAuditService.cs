using Notebook.Core.Types;

namespace Notebook.Server.Services;

public interface IAuditService
{
    void Log(AuditEvent auditEvent);
}
