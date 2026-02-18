using Microsoft.Extensions.Options;
using ThinkerAgent.Configuration;
using ThinkerAgent.Services;

namespace ThinkerAgent;

public static class DependencyInjection
{
    public static IServiceCollection AddThinkerServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ThinkerOptions>(configuration.GetSection(ThinkerOptions.SectionName));

        services.AddSingleton<WorkerState>();

        services.AddHttpClient<NotebookApiClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptionsMonitor<ThinkerOptions>>().CurrentValue;
            client.BaseAddress = new Uri(opts.ServerUrl.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.Token);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Named HttpClient for LLM — handler pooling without baked-in BaseAddress.
        services.AddHttpClient("LlmClient");

        // Transient factory: reads current options on every resolution so URL/ApiType/ApiKey
        // changes take effect immediately (workers create a new scope each iteration).
        services.AddTransient<ILlmClient>(sp =>
        {
            var opts = sp.GetRequiredService<IOptionsMonitor<ThinkerOptions>>().CurrentValue;
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var client = factory.CreateClient("LlmClient");
            client.BaseAddress = new Uri(opts.LlmUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromMinutes(5);

            if (opts.ApiType == ApiType.OpenAi)
            {
                if (!string.IsNullOrWhiteSpace(opts.ApiKey))
                {
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.ApiKey);
                }
                return new OpenAiLlmClient(client);
            }

            return new OllamaClient(client);
        });

        services.AddHostedService<RobotWorkerService>();

        return services;
    }
}
