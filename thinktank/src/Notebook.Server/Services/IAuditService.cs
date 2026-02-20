namespace Notebook.Server.Services;

public interface IAuditService
{
    void Log(byte[] actor, string action, string resource, object? detail = null, string? ip = null, string? userAgent = null);
}
