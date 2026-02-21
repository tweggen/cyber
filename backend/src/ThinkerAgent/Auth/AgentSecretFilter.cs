using Microsoft.Extensions.Options;
using ThinkerAgent.Configuration;

namespace ThinkerAgent.Auth;

public class AgentSecretFilter(IOptionsSnapshot<ThinkerOptions> options) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var secret = options.Value.AgentSecret;

        // Empty secret = open (dev mode)
        if (string.IsNullOrEmpty(secret))
            return await next(context);

        var header = context.HttpContext.Request.Headers["X-Agent-Secret"].FirstOrDefault();

        if (header != secret)
            return Results.Unauthorized();

        return await next(context);
    }
}
