using Microsoft.Extensions.Http.Resilience;
using LocalAIAgent.Agent.Services.Options;
using Polly;

namespace LocalAIAgent.Agent.Services;

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
            // Circuit breaker sampling duration must be at least 2x the attempt timeout (default 10s)
            resilienceOptions.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
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

        // T046: OpenMeteoAllergenClient
        services.AddHttpClient<OpenMeteoAllergenClient>(client =>
        {
            client.BaseAddress = new Uri(options.AirQualityBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        })
        .AddStandardResilienceHandler(resilienceOptions =>
        {
            resilienceOptions.Retry.MaxRetryAttempts = options.MaxRetryAttempts;
            resilienceOptions.Retry.BackoffType = DelayBackoffType.Exponential;
        });

        return services;
    }
}
