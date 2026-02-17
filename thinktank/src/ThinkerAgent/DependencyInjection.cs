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

        services.AddHttpClient<IOllamaClient, OllamaClient>(client =>
        {
            client.BaseAddress = new Uri(options.OllamaUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        services.AddHostedService<RobotWorkerService>();

        return services;
    }
}
