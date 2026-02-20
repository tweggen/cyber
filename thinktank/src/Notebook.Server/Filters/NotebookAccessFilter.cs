using Notebook.Data.Repositories;
using Notebook.Server.Services;

namespace Notebook.Server.Filters;

public class NotebookAccessFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        // Extract notebookId from route values
        if (!httpContext.Request.RouteValues.TryGetValue("notebookId", out var notebookIdObj)
            || !Guid.TryParse(notebookIdObj?.ToString(), out var notebookId))
        {
            return Results.NotFound();
        }

        // Extract authorId from JWT sub claim
        var authorHex = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(authorHex))
            return Results.Unauthorized();

        var authorId = Convert.FromHexString(authorHex);

        var accessRepo = httpContext.RequestServices.GetRequiredService<IAccessRepository>();
        var auditService = httpContext.RequestServices.GetRequiredService<IAuditService>();

        // Owner always has full access
        if (await accessRepo.IsOwnerAsync(notebookId, authorId, httpContext.RequestAborted))
            return await next(context);

        // Check ACL
        var access = await accessRepo.GetAccessAsync(notebookId, authorId, httpContext.RequestAborted);
        if (access is null)
        {
            auditService.Log(authorId, "access.denied", $"notebook:{notebookId}",
                new { reason = "no_acl" },
                httpContext.Connection.RemoteIpAddress?.ToString(),
                httpContext.Request.Headers.UserAgent.ToString());
            return Results.NotFound();
        }

        var method = httpContext.Request.Method;
        var needsWrite = method is "POST" or "PUT" or "PATCH" or "DELETE";

        if (needsWrite && !access.Write)
        {
            auditService.Log(authorId, "access.denied", $"notebook:{notebookId}",
                new { reason = "write_required", method },
                httpContext.Connection.RemoteIpAddress?.ToString(),
                httpContext.Request.Headers.UserAgent.ToString());
            return Results.NotFound();
        }

        if (!needsWrite && !access.Read)
        {
            auditService.Log(authorId, "access.denied", $"notebook:{notebookId}",
                new { reason = "read_required", method },
                httpContext.Connection.RemoteIpAddress?.ToString(),
                httpContext.Request.Headers.UserAgent.ToString());
            return Results.NotFound();
        }

        return await next(context);
    }
}
