using ThinkerAgent.Configuration;
using ThinkerAgent.Services;

namespace ThinkerAgent;

public static class DependencyInjection
{
    public static IServiceCollection AddThinkerServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ThinkerOptions>(configuration.GetSection(ThinkerOptions.SectionName));

        var options = new ThinkerOptions();
        configuration.GetSection(ThinkerOptions.SectionName).Bind(options);

        services.AddSingleton<WorkerState>();

        services.AddHttpClient<NotebookApiClient>(client =>
        {
            client.BaseAddress = new Uri(options.ServerUrl.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.Token);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        switch (options.ApiType)
        {
            case ApiType.OpenAi:
                services.AddHttpClient<ILlmClient, OpenAiLlmClient>(client =>
                {
                    client.BaseAddress = new Uri(options.LlmUrl.TrimEnd('/') + "/");
                    client.Timeout = TimeSpan.FromMinutes(5);
                    if (!string.IsNullOrWhiteSpace(options.ApiKey))
                    {
                        client.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.ApiKey);
                    }
                });
                break;

            default:
                services.AddHttpClient<ILlmClient, OllamaClient>(client =>
                {
                    client.BaseAddress = new Uri(options.LlmUrl.TrimEnd('/') + "/");
                    client.Timeout = TimeSpan.FromMinutes(5);
                });
                break;
        }

        services.AddHostedService<RobotWorkerService>();

        return services;
    }
}
