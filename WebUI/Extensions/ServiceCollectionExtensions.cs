using WebUI.Configuration;
using WebUI.Services;

namespace WebUI.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGoBikeApiClient(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.Configure<ApiSettings>(configuration.GetSection(ApiSettings.SectionName));
        services.AddScoped<IApiCookieAccessor, ApiCookieAccessor>();
        services.AddTransient<ApiAuthCookieHandler>();

        var httpClientBuilder = services.AddHttpClient<IGoBikeApiClient, GoBikeApiClient>((sp, client) =>
        {
            var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ApiSettings>>().Value;
            client.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 10, 300));
        }).AddHttpMessageHandler<ApiAuthCookieHandler>();

        httpClientBuilder.ConfigurePrimaryHttpMessageHandler(() =>
        {
            var handler = new HttpClientHandler
            {
                UseCookies = false
            };

            if (environment.IsDevelopment())
            {
                handler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }

            return handler;
        });

        return services;
    }
}
