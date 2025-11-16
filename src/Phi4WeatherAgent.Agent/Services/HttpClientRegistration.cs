using Microsoft.Extensions.Http.Resilience;
using Phi4WeatherAgent.Agent.Services.Options;
using Polly;

namespace Phi4WeatherAgent.Agent.Services;

/// <summary>
/// Extension methods for registering OpenMeteo HTTP clients with Polly resilience.
/// </summary>
public static class HttpClientRegistration
{
    /// <summary>
    /// Registers all OpenMeteo HTTP clients with resilience policies.
    /// </summary>
    public static IServiceCollection AddOpenMeteoClients(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind OpenMeteo configuration
        services.Configure<OpenMeteoOptions>(configuration.GetSection(OpenMeteoOptions.SectionName));
        var options = configuration.GetSection(OpenMeteoOptions.SectionName).Get<OpenMeteoOptions>() 
            ?? new OpenMeteoOptions();

        // T033: OpenMeteoGeocodeClient
        services.AddHttpClient<OpenMeteoGeocodeClient>(client =>
        {
            client.BaseAddress = new Uri(options.GeocodeBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        })
        .AddStandardResilienceHandler(resilienceOptions =>
        {
            resilienceOptions.Retry.MaxRetryAttempts = options.MaxRetryAttempts;
            resilienceOptions.Retry.BackoffType = DelayBackoffType.Exponential;
            resilienceOptions.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);
        });

        // T034: OpenMeteoWeatherClient
        services.AddHttpClient<OpenMeteoWeatherClient>(client =>
        {
            client.BaseAddress = new Uri(options.WeatherBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        })
        .AddStandardResilienceHandler(resilienceOptions =>
        {
            resilienceOptions.Retry.MaxRetryAttempts = options.MaxRetryAttempts;
            resilienceOptions.Retry.BackoffType = DelayBackoffType.Exponential;
        });

        // TODO T046: Register OpenMeteoAllergenClient
        // services.AddHttpClient<OpenMeteoAllergenClient>(client =>
        // {
        //     client.BaseAddress = new Uri(options.AirQualityBaseUrl);
        //     client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        // })
        // .AddStandardResilienceHandler(resilienceOptions =>
        // {
        //     resilienceOptions.Retry.MaxRetryAttempts = options.MaxRetryAttempts;
        //     resilienceOptions.Retry.BackoffType = DelayBackoffType.Exponential;
        // });

        return services;
    }
}
