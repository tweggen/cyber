using System.Text.Json;
using Notebook.Core.Types;
using Notebook.Server.Services;

namespace Notebook.Server.Auth;

public static class AuditHelper
{
    public static void LogAction(
        IAuditService audit,
        HttpContext? httpContext,
        string action,
        Guid? notebookId = null,
        string? targetType = null,
        string? targetId = null,
        object? detail = null)
    {
        byte[]? authorId = null;
        string? ipAddress = null;
        string? userAgent = null;

        if (httpContext is not null)
        {
            var sub = httpContext.User.FindFirst("sub")?.Value;
            if (!string.IsNullOrEmpty(sub))
                authorId = Convert.FromHexString(sub);

            ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
            userAgent = httpContext.Request.Headers.UserAgent.FirstOrDefault();
        }

        JsonElement? detailElement = null;
        if (detail is not null)
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(detail);
            detailElement = JsonDocument.Parse(json).RootElement.Clone();
        }

        audit.Log(new AuditEvent
        {
            NotebookId = notebookId,
            AuthorId = authorId,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            Detail = detailElement,
            IpAddress = ipAddress,
            UserAgent = userAgent,
        });
    }
}
